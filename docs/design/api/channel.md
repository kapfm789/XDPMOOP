# API: channel

Tiền tố `/api/channel`. Use case ở [channel-realtime.md](../../usecase-userstory/channel-realtime.md); bảng ở [data-model/channel.md](../data-model/channel.md); trình tự ở [flows/webhook-ingestion.md](../flows/webhook-ingestion.md). Quy ước chung ở [README.md](README.md).

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/channel/webhooks/{channel}` | Công khai, kiểm chữ ký | Header `X-Signature`; body theo kênh | 202: `eventId`, `status` | 401 `invalid_signature`; 404 khi shop chưa cấu hình |
| GET | `/api/channel/channel-shops` | Owner | | Danh sách shop | |
| POST | `/api/channel/channel-shops` | Owner | `channel`, `shopId`, `branchId` | 201: shop kèm `secret` | 409 `duplicate` |
| PATCH | `/api/channel/channel-shops/{id}/active` | Owner | `isActive` | 200: shop | |
| GET | `/api/channel/webhook-events` | Owner | `channel?`, `status?`, `page`, `pageSize` | Danh sách webhook đã nhận | |
| POST | `/api/channel/simulator/burst` | Owner | `channel`, `shopId`, `skuCode`, `quantityPerOrder`, `orders`, `concurrency` | 202: `burstId`, số webhook đã gửi | |

`{channel}` nhận `shopee`, `tiktok`, `lazada`. `secret` chỉ được trả một lần lúc tạo shop.

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
