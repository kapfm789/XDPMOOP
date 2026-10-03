---
name: scaffold-service
description: "Dựng skeleton cho một service backend của OISM (identity, catalog, core, channel, insights) theo đúng khuôn bốn lớp Clean Architecture, kèm hai project test, Dockerfile và đăng ký vào solution, gateway, Compose. Dùng skill này khi người dùng nói 'dựng service', 'tạo skeleton', 'scaffold', 'khởi tạo project cho service', hoặc khi một task yêu cầu service mà folder của nó chưa tồn tại. Không dùng để viết nghiệp vụ; việc đó thuộc implement-task và add-use-case."
argument-hint: "[tên service: identity | catalog | core | channel | insights]"
---

# Dựng skeleton một service

Service cần dựng: $ARGUMENTS

Mọi service của OISM có cùng một khuôn để người và công cụ đọc service này xong là viết được service kia. Skill này chỉ dựng khung rỗng chạy được; nó không thêm entity, bảng hay endpoint nghiệp vụ nào.

## Trước khi dựng

1. Đọc `docs/architecture/source-tree.md` (tên folder, project, namespace) và `docs/architecture/layering.md` (lớp nào tham chiếu lớp nào).
2. Đọc `docs/architecture/context-and-containers.md` để lấy tiền tố route, cổng dev và tên schema của service.
3. Kiểm `backend/Oism.sln`, `backend/shared/Oism.BuildingBlocks` và `backend/shared/Oism.Contracts` đã có chưa. Chưa có thì đó là phần còn lại của task W1-02; dựng chúng trước theo `docs/plans/backend-shared.md`, hoặc báo lại nếu người dùng chỉ muốn một service.
4. Tên service phải là một trong năm tên ở trên. Tên khác thì hỏi lại; thêm service mới là một quyết định kiến trúc cần ADR.

## Thứ cần tạo

Với `<service>` viết thường và `<Service>` viết hoa chữ đầu:

```text
backend/services/<service>/
├─ src/
│  ├─ Oism.<Service>.Domain/            không tham chiếu project nào
│  ├─ Oism.<Service>.Application/       tham chiếu Domain, Oism.Contracts
│  ├─ Oism.<Service>.Infrastructure/    tham chiếu Application, Domain, Oism.BuildingBlocks, Oism.Contracts
│  └─ Oism.<Service>.Api/               tham chiếu Application, Infrastructure
│     ├─ Controllers/
│     ├─ Consumers/
│     ├─ Program.cs
│     └─ Dockerfile
└─ tests/
   ├─ Oism.<Service>.UnitTests/         tham chiếu Domain, Application
   └─ Oism.<Service>.IntegrationTests/  tham chiếu Api
```

Nội dung tối thiểu:

| Nơi | Có gì |
| --- | --- |
| Infrastructure | `<Service>DbContext` kế thừa DbContext base của `Oism.BuildingBlocks`; cấu hình Npgsql với snake_case, schema mặc định `<service>` và bảng lịch sử migration trong schema đó; thư mục `Migrations` với migration đầu tạo `outbox_messages` và `inbox_messages` nếu service có phát hoặc nhận event; phương thức mở rộng đăng ký DI |
| Api | `Program.cs` đăng ký: xác thực JWT bằng khóa công khai, tenant middleware, ProblemDetails, Swagger, health check tại `/health`, chạy migration khi khởi động ở môi trường dev |
| IntegrationTests | Lớp nền dùng `WebApplicationFactory` và Testcontainers PostgreSQL; một test gọi `/health`; một test duyệt mọi entity của DbContext và báo lỗi nếu entity nghiệp vụ nào không cài `ITenantOwned` |
| UnitTests | Một test giữ chỗ để project build và chạy được |

Service nào phát hoặc nhận event thì xem bảng ở `docs/design/events.md`.

## Nối vào phần còn lại

1. Thêm 6 project vào `backend/Oism.sln`.
2. Thêm route `/api/<service>` vào cấu hình của `backend/gateway`, bỏ tiền tố khi chuyển tiếp.
3. Thêm service vào `deploy/docker-compose.yml` với đúng tên và cổng ở tài liệu; thêm schema `<service>` vào `deploy/postgres/init-schemas.sql`.
4. Thêm project vào CI nếu CI liệt kê project tường minh.

Nếu gateway hoặc Compose chưa tồn tại, bỏ qua bước tương ứng và ghi rõ trong báo cáo.

## Kiểm

- `dotnet build backend/Oism.sln` không lỗi, không cảnh báo.
- `dotnet test` cho hai project test của service chạy xanh; test tích hợp cần Docker.
- Kiểm tham chiếu: Domain không tham chiếu gì; Application không tham chiếu Infrastructure hay Api.

## Báo cáo

Liệt kê project đã tạo, chỗ đã nối (solution, gateway, Compose, CI), lệnh đã chạy kèm kết quả, và bước nào bị bỏ qua vì phần phụ thuộc chưa có. Không commit.
