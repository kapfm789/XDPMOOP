# Quy ước backend

Mọi service viết theo cùng một khuôn để người và công cụ AI đọc service này xong là viết được service kia. Phân lớp ở [architecture/layering.md](../architecture/layering.md); tên folder và project ở [architecture/source-tree.md](../architecture/source-tree.md).

## Nền

- C# 12, .NET 8. Bật nullable; cảnh báo là lỗi; namespace theo file.
- Mọi lời gọi I/O là `async` và nhận `CancellationToken`.
- Phiên bản package khai báo một chỗ ở `backend/Directory.Packages.props`.

## Khuôn của một use case

Một use case là một thư mục trong Application gồm command hoặc query, handler và validator. Handler là lớp thường, đăng ký thẳng vào DI; dự án không dùng thư viện mediator.

```csharp
namespace Oism.Core.Application.Orders.ConfirmOrder;

public sealed record ConfirmOrderCommand(Guid OrderId);

public sealed class ConfirmOrderHandler(
    IUnitOfWork unitOfWork,
    IOrderRepository orders,
    IStockService stock,
    IEventPublisher events,
    IClock clock)
{
    public async Task<OrderDto> Handle(ConfirmOrderCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var order = await orders.GetForUpdateAsync(command.OrderId, ct)
            ?? throw new NotFoundException("order");

        order.Confirm(clock.UtcNow);
        var costs = await stock.ConsumeAsync(order, ct);
        order.SnapshotCosts(costs);
        events.Enqueue(OrderConfirmedFactory.From(order));

        await transaction.CommitAsync(ct);
        return OrderDto.From(order);
    }
}
```

Điều khuôn này bắt buộc:

1. Handler mở đúng một transaction và commit ở cuối. Ném lỗi thì transaction tự rollback khi bị hủy.
2. Chứng từ được nạp bằng phương thức có khóa (`GetForUpdateAsync`) trước mọi kiểm tra trạng thái.
3. Quy tắc nghiệp vụ nằm trong phương thức của entity (`order.Confirm`), không nằm trong handler.
4. Event được xếp vào outbox qua `IEventPublisher.Enqueue`, không gửi thẳng lên RabbitMQ.
5. Handler trả DTO, không trả entity.

## Controller

```csharp
[ApiController]
[Route("orders")]
public sealed class OrdersController(ConfirmOrderHandler confirmOrder) : ControllerBase
{
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = Policies.OwnerOrStaff)]
    public async Task<ActionResult<OrderDto>> Confirm(Guid id, CancellationToken ct) =>
        Ok(await confirmOrder.Handle(new ConfirmOrderCommand(id), ct));
}
```

- Route không mang tiền tố `/api/<service>`; gateway đã bỏ tiền tố đó.
- Mỗi action khai báo policy. Không khai báo thì mặc định bị từ chối.
- Controller không có `try/catch`; lỗi do middleware chung đổi thành ProblemDetails.

## Lỗi

Domain và Application ném exception có mã; middleware trong `Oism.BuildingBlocks/Web` đổi sang phản hồi theo [design/api/README.md](../design/api/README.md).

| Exception | HTTP | `code` |
| --- | --- | --- |
| `ValidationException` | 400 | `validation_failed` |
| `UnauthenticatedException` | 401 | `unauthenticated` |
| `NotFoundException` | 404 | `not_found` |
| `DuplicateException` | 409 | `duplicate` |
| `CategoryInUseException` | 409 | `category_in_use` |
| `InvalidStateTransitionException` | 409 | `invalid_state_transition` |
| `InsufficientStockException` | 409 | `insufficient_stock` |
| `StocktakeBelowReservedException` | 409 | `stocktake_below_reserved` |
| `ReferenceNotReadyException` | 409 | `reference_not_ready` |
| Exception khác | 500 | Không lộ chi tiết; ghi log kèm correlation id |

Không dùng exception cho luồng bình thường (ví dụ "không có kết quả tìm kiếm").

Ba điểm của khuôn đã có ở `catalog`, service khác chép theo:

- Trùng dữ liệu do chỉ mục unique của database quyết định, không kiểm trước bằng một câu truy vấn: `ITransaction.CommitAsync` đổi lỗi vi phạm unique của PostgreSQL thành `DuplicateException`. Nhờ vậy hai request tới cùng lúc vẫn nhận đúng 409.
- Quy tắc cần trả 400 mà phải đọc dữ liệu mới kiểm được (ví dụ vòng lặp cha con của danh mục): Domain cung cấp hàm kiểm thuần, handler gọi nó rồi ném `ValidationException` của FluentValidation kèm tên trường.
- `Program.cs` đăng ký mọi lớp có tên kết thúc bằng `Handler` trong assembly Application; thêm use case không cần sửa `Program.cs`.

## EF Core

- Ánh xạ bằng lớp `IEntityTypeConfiguration<T>` ở Infrastructure. Entity ở Domain không mang attribute của EF Core.
- Tên bảng và cột là snake_case, qua `UseSnakeCaseNamingConvention`.
- Entity nghiệp vụ cài `ITenantOwned`; DbContext kế thừa DbContext base của `Oism.BuildingBlocks` để có Global Query Filter và interceptor.
- DbContext đặt schema mặc định là tên service bằng `HasDefaultSchema` ([ADR-0013](../decisions/0013-one-database-schema-per-service.md)). Không entity hay truy vấn nào trỏ sang schema của service khác.
- Truy vấn chỉ đọc dùng `AsNoTracking` và chiếu thẳng sang DTO.
- Không bật lazy loading.
- SQL thô chỉ dùng cho khóa dòng và cho migration (CHECK, trigger); luôn qua tham số, không nối chuỗi.
- `IgnoreQueryFilters` chỉ được xuất hiện ở các phương thức liệt kê trong [multi-tenancy.md](../architecture/multi-tenancy.md).

## Migration

- Mỗi service giữ migration của mình trong `Oism.<Service>.Infrastructure/Migrations`.
- Bảng lịch sử migration nằm trong schema của service: `MigrationsHistoryTable("__EFMigrationsHistory", "<service>")`. Để mặc định thì 5 service dùng chung một bảng lịch sử.
- Một PR tối đa một migration cho mỗi service. Không sửa migration đã merge; sai thì thêm migration mới.
- CHECK, trigger và chỉ mục đặc biệt viết bằng `migrationBuilder.Sql` trong chính migration tạo bảng.
- Service tự chạy migration khi khởi động ở môi trường dev và demo.

## Log

- Log có cấu trúc; mỗi dòng mang correlation id, `tenantId` và tên use case.
- Không ghi mật khẩu, token, khóa, hay toàn bộ payload chứa dữ liệu khách hàng.
- Lỗi nghiệp vụ (4xx) ghi mức Information; lỗi hệ thống ghi mức Error.

## Package

Danh sách đã chọn; thêm package ngoài danh sách phải hỏi nhóm trước.

| Việc | Package |
| --- | --- |
| ORM và PostgreSQL | Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL, EFCore.NamingConventions |
| Kiểm tra đầu vào | FluentValidation |
| Băm mật khẩu | BCrypt.Net-Next |
| Xác thực | Microsoft.AspNetCore.Authentication.JwtBearer |
| Thông điệp | RabbitMQ.Client; outbox và inbox tự viết trong `Oism.BuildingBlocks` |
| Job nền | Hangfire.AspNetCore, Hangfire.PostgreSql |
| Gateway | Yarp.ReverseProxy |
| Tài liệu API | Swashbuckle.AspNetCore |
| Test | xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, Microsoft.AspNetCore.Mvc.Testing, Testcontainers.PostgreSql, Testcontainers.RabbitMq, coverlet.collector |

Dự án không dùng MediatR, AutoMapper, MassTransit và FluentAssertions: các thư viện này đã chuyển sang mô hình giấy phép thương mại ở những bản phát hành gần đây và dự án không cần tới chúng. Ánh xạ giữa entity và DTO viết tay.

## Điều không làm

- Không đặt nghiệp vụ trong controller, consumer, EF configuration hay SQL.
- Không gọi HTTP sang service khác; không đọc schema của service khác.
- Không đổi `on_hand`, `reserved` ngoài `PostLedger` và `IStockService`.
- Không dùng `DateTime.Now` hay `DateTime.UtcNow` ngoài lớp cài `IClock`.
- Không nuốt exception; không `catch` rồi tiếp tục commit.
