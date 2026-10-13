# API: channel

Tiền tố `/api/channel`. Use case ở [channel-realtime.md](../../usecase-userstory/channel-realtime.md); bảng ở [data-model/channel.md](../data-model/channel.md); trình tự ở [flows/webhook-ingestion.md](../flows/webhook-ingestion.md). Quy ước chung ở [README.md](README.md).

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/channel/webhooks/{channel}` | Công khai, kiểm chữ ký | Header `X-Signature`; body theo kênh | 202: `eventId`, `status` | 401 `invalid_signature`; 404 khi shop chưa cấu hình |
| GET | `/api/channel/channel-shops` | Owner | | Danh sách shop | |
| POST | `/api/channel/channel-shops` | Owner | `channel`, `shopId`, `branchId` | 201: shop kèm `secret` | 409 `duplicate` |
| PATCH | `/api/channel/channel-shops/{id}/active` | Owner | `isActive` | 200: shop | |
| GET | `/api/channel/webhook-events` | Owner | `channel?`, `status?`, `page`, `pageSize` | Danh sách webhook đã nhận | |
| POST | `/api/channel/simulator/burst` | Owner | `channel`, `shopId`, `skuCode`, `quantityPerOrder`, `orders`, `concurrency`, `unitPrice?` | 202: `burstId`, `sent` | 404 khi shop không có trong tenant |

`{channel}` nhận `shopee`, `tiktok`, `lazada`. `secret` chỉ được trả một lần lúc tạo shop.

- Tên kênh ở đường dẫn, trong body và ở bộ lọc không phân biệt hoa thường; phản hồi luôn viết `Shopee`, `TikTok`, `Lazada`. Kênh ngoài ba giá trị này: webhook trả 404, các endpoint khác trả 400 `validation_failed`.
- Webhook: body không phải JSON, hoặc thiếu mã sự kiện hay mã shop, trả 400 `validation_failed` và không lưu gì, vì không biết webhook của ai và không chống trùng được. Shop chưa cấu hình, shop của kênh khác hoặc shop đã tắt trả 404. Chữ ký sai, viết hoa hoặc thiếu header trả 401 và không lưu gì.
- Webhook hợp lệ trả 202 với `eventId` là mã sự kiện của sàn và `status` của bản ghi: `Submitted` khi đã ghi `SubmitOrder` vào outbox, `Failed` khi phần đơn trong payload không đổi được về Canonical Order, và `Reserved` khi đơn đó đã được giữ hàng qua một webhook khác mã sự kiện. Gửi lại cùng mã sự kiện trả 202 với `status` hiện có của bản ghi đã lưu, có thể đã là `Reserved` hoặc `Rejected`.
- Một dòng của Canonical Order phải có mã SKU, `quantity` dương, đơn giá không âm, giảm giá không âm và không vượt `quantity × unitPrice`; đơn phải có mã đơn, thời điểm đặt và ít nhất một dòng. Giảm giá bỏ trống là 0. Sai một điều thì bản ghi là `Failed` kèm `reason`, để đơn sai không tới `core`.
- Shop trả về gồm `id`, `channel`, `shopId`, `branchId`, `isActive`; riêng phản hồi lúc tạo có thêm `secret`, là 32 byte ngẫu nhiên viết hex. Danh sách xếp theo `channel` rồi `shopId`. `shopId` bị cắt khoảng trắng hai đầu, không rỗng, tối đa 100 ký tự; `channel` ngoài ba kênh, `shopId` rỗng hoặc thiếu `branchId` trả 400 `validation_failed`. Mã shop đã có ở bất kỳ tenant nào trong cùng kênh trả 409 `duplicate`.
- `channel` không giữ bản sao chi nhánh nên không kiểm `branchId` lúc tạo shop: chi nhánh không có hoặc đã tắt thì `core` từ chối đơn của shop bằng `OrderRejected` với lý do `InactiveBranch`.
- `PATCH .../active`: `id` không có trong tenant trả 404 `not_found`. Shop bị tắt coi như không có với webhook.
- `GET /webhook-events` trả theo khuôn phân trang chung, mới nhận nhất trước. Mỗi dòng gồm `id`, `channel`, `eventId`, `externalOrderId?`, `status`, `reason?`, `orderId?`, `receivedAt`, `updatedAt`; payload không nằm trong danh sách. `reason` là mã lý do của `OrderRejected` khi `Rejected`, và là câu mô tả lỗi của payload khi `Failed`. `status` ngoài năm trạng thái, `page` nhỏ hơn 1 hoặc `pageSize` ngoài khoảng 1 đến 100 trả 400 `validation_failed`.
- Bắn tải: `orders` từ 1 đến 1000 là số webhook sẽ gửi, `concurrency` từ 1 đến 100 là số webhook gửi cùng lúc, `quantityPerOrder` dương; sai thì 400 `validation_failed`. `unitPrice` bỏ trống là 0, vì `channel` không biết giá của SKU. Mỗi webhook mang một mã sự kiện và một mã đơn riêng, được ký bằng `secret` của shop và gửi vào chính endpoint webhook. Phản hồi trả về sau khi mọi webhook đã được gửi, với `sent` là số webhook đã gửi; kết quả giữ hàng của từng đơn xem ở `GET /webhook-events`.
- Service gọi chính endpoint webhook của nó theo địa chỉ ở cấu hình `Simulator:WebhookBaseUrl`, mặc định `http://localhost:8080/` là cổng service nghe trong container.

## Chữ ký

`X-Signature` là HMAC-SHA256 của nguyên văn body với `secret` của shop, viết dạng hex chữ thường. Đây là cơ chế giả lập do nhóm đặt ra, không phải cách ký thật của sàn nào.

## Hình dạng payload giả lập

Ba kênh cố ý có hình dạng khác nhau để adapter có việc phải làm. Các hình dạng này do nhóm tự đặt cho simulator, không sao chép API thật của sàn.

Shopee:

```json
{
  "event_id": "sp-000123",
  "shop_id": "shopee-shop-01",
  "ordersn": "SP2610200001",
  "create_time": 1792466127,
  "item_list": [{ "model_sku": "AO-A-DEN-M", "quantity": 1, "price": 150000, "discount": 0 }]
}
```

TikTok:

```json
{
  "id": "tt-evt-000123",
  "shop_id": "tiktok-shop-01",
  "data": {
    "order_id": "TT2610200001",
    "create_time": 1792466127,
    "line_items": [{ "seller_sku": "AO-A-DEN-M", "quantity": 1, "sale_price": 150000, "seller_discount": 0 }]
  }
}
```

Lazada:

```json
{
  "message_id": "lz-000123",
  "seller_id": "lazada-seller-01",
  "order": {
    "order_number": "LZ2610200001",
    "created_at": "2026-10-20T03:15:27Z",
    "items": [{ "sku": "AO-A-DEN-M", "qty": 1, "paid_price": 150000, "voucher": 0 }]
  }
}
```

## Ánh xạ về Canonical Order

| Canonical | Shopee | TikTok | Lazada |
| --- | --- | --- | --- |
| Mã sự kiện | `event_id` | `id` | `message_id` |
| Mã shop | `shop_id` | `shop_id` | `seller_id` |
| `externalOrderId` | `ordersn` | `data.order_id` | `order.order_number` |
| `placedAt` | `create_time`, giây epoch | `data.create_time`, giây epoch | `order.created_at`, ISO 8601 |
| `lines[].skuCode` | `item_list[].model_sku` | `data.line_items[].seller_sku` | `order.items[].sku` |
| `lines[].quantity` | `quantity` | `quantity` | `qty` |
| `lines[].unitPrice` | `price` | `sale_price` | `paid_price` |
| `lines[].discount` | `discount` | `seller_discount` | `voucher` |

## Đường vào không phải HTTP

| Nguồn | Tên | Xử lý |
| --- | --- | --- |
| RabbitMQ | `OrderReserved` | Đặt bản ghi webhook sang Reserved, ghi `order_id` |
| RabbitMQ | `OrderRejected` | Đặt bản ghi webhook sang Rejected, ghi lý do |
