# backend/shared — kế hoạch folder

Ba thư viện dùng chung cho cả 5 service: `Oism.SharedKernel` (kiểu nền cho Domain), `Oism.BuildingBlocks` (hạ tầng) và `Oism.Contracts` (hợp đồng event). Owner: Dev A (BuildingBlocks), Dev B (outbox/inbox, Contracts). Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Thuộc tầng nào

Tầng dịch vụ và tầng thông điệp (mục 4.1 của kế hoạch tổng). Mọi service tham chiếu các project này; chúng không tham chiếu service nào.

## Cấu trúc

```text
shared/
├─ Oism.SharedKernel/ ITenantOwned, OismException, NotFoundException, DuplicateException; không phụ thuộc gì
├─ Oism.BuildingBlocks/
│  ├─ Tenancy/        ITenantContext, Global Query Filter, middleware đọc TenantId từ JWT
│  ├─ Auth/           kiểm JWT, policy theo vai trò Owner, Staff, Cashier
│  ├─ Messaging/      OutboxMessage, InboxMessage, publisher nền, consumer base bỏ qua bản trùng
│  ├─ Persistence/    DbContext base, interceptor gán TenantId khi ghi
│  └─ Web/            ProblemDetails, correlation id, health check
├─ Oism.Contracts/    BranchUpserted, SkuUpserted, SubmitOrder, OrderReserved, OrderRejected,
│                     OrderConfirmed, OrderCancelled, StockChanged
└─ tests/
   └─ Oism.BuildingBlocks.IntegrationTests/   Tenancy, Web, Messaging; chạy trên PostgreSQL thật
```

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 1 | W1-02 | Skeleton repo, `Oism.BuildingBlocks` (tenant context, Global Query Filter, kiểm JWT, lỗi chuẩn), mẫu service bốn lớp | A | NFR-MAINT-01, NFR-TENANT-01 | `dotnet build` xanh; test: entity có TenantId tự bị lọc |
| 1 | W1-13 | shared: outbox và inbox trong `Oism.BuildingBlocks`, kết nối RabbitMQ, `Oism.Contracts` với `BranchUpserted` và `SkuUpserted` | B | NFR-SEC-02 | Event mẫu đi từ outbox tới consumer; gửi lại không xử lý trùng |

## Quy tắc riêng

- Không chứa nghiệp vụ của bất kỳ service nào.
- Đổi `Oism.Contracts` chỉ được thêm trường; PR cần cả bên phát và bên nhận duyệt.
- Mọi consumer kế thừa consumer base để ghi `eventId` vào inbox trước khi xử lý (kịch bản T22).
- Mọi event mang `eventId`, `tenantId`, `occurredAt`; consumer base đặt lại ngữ cảnh tenant từ `tenantId`.
