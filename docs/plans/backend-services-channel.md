# backend/services/channel — kế hoạch folder

Nhận webhook giả lập của Shopee, TikTok, Lazada, chống trùng, chuẩn hóa về Canonical Order rồi gửi `SubmitOrder` cho `core` (FR-SIM-01, phần adapter của FR-ORD-01). Owner: Dev B. Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Phân lớp

```text
channel/
├─ src/
│  ├─ Oism.Channel.Domain/           WebhookEvent, ChannelShop, CanonicalOrder
│  ├─ Oism.Channel.Application/      ReceiveWebhook, adapter Shopee, TikTok, Lazada, SimulateBurst
│  ├─ Oism.Channel.Infrastructure/   ChannelDbContext, migration, outbox, inbox
│  └─ Oism.Channel.Api/              WebhooksController, SimulatorController, consumer OrderReserved, OrderRejected
└─ tests/
   ├─ Oism.Channel.UnitTests/           ánh xạ payload của từng sàn
   └─ Oism.Channel.IntegrationTests/    webhook gửi lại, bắn tải
```

Phụ thuộc chỉ hướng vào trong: Api → Application → Domain; Infrastructure cài đặt interface của Application.

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 6 | W3-03 | channel: nhận webhook ba sàn, chống trùng, chuẩn hóa, gửi `SubmitOrder`; công cụ bắn tải | B | FR-SIM-01, FR-ORD-01 | Gửi lại cùng webhook không tạo đơn thứ hai; bắn được 50 đơn đồng thời |
| 9 | W5-02 | Bộ test cách ly tenant toàn hệ thống: API, event, SignalR, job | A | NFR-TENANT-01 | Mọi ca dùng ID tenant khác nhận 404 hoặc 403 |

Skeleton của folder được tạo từ mẫu ở phase 1 (W1-02) để gateway định tuyến được; nghiệp vụ bắt đầu ở phase 6.

## Sở hữu

- Database `oism_channel`: ChannelShop, WebhookEvent (mục 5 của kế hoạch tổng).
- API: `/webhooks/{channel}`, `/simulator/burst`, `/webhook-events` (mục 8).
- Event phát: `SubmitOrder`. Event nhận: `OrderReserved`, `OrderRejected` (mục 7).
- Kiểm thử: T03.

## Quy tắc riêng

- Không gọi API thật của sàn (D-09).
- Unique (TenantId, Channel, EventId) trên `WebhookEvent` là hàng rào chống trùng đầu tiên; `core` có hàng rào thứ hai trên (TenantId, Channel, ExternalOrderId).
- `ChannelShop` ánh xạ shop của sàn sang tenant và chi nhánh, vì webhook không mang JWT.
- Nếu phải lui về phương án 3 service (mục 11), folder này gộp vào `core` thành module Channel.
