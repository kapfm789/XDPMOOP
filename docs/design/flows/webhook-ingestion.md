# Luồng: nhận webhook

`channel` nhận webhook, kiểm chữ ký, chống trùng, đổi payload của từng kênh về Canonical Order rồi gửi command `SubmitOrder` cho `core`. Webhook được trả lời ngay bằng 202; kết quả giữ hàng về sau bằng event. Use case [UC-SIM-01](../../usecase-userstory/channel-realtime.md) và [UC-ORD-01](../../usecase-userstory/orders.md); payload ở [api/channel.md](../api/channel.md).

```mermaid
sequenceDiagram
  autonumber
  participant Sim as Simulator
  participant Gw as gateway
  participant Ch as channel
  participant CDB as schema channel
  participant MQ as RabbitMQ
  participant Core as core
  participant Ins as insights

  Sim->>Gw: POST api/channel/webhooks/shopee
  Gw->>Ch: chuyển tiếp, không kiểm JWT
  Ch->>CDB: tìm ChannelShop theo kênh và mã shop
  alt không có shop hoặc chữ ký sai
    Ch-->>Sim: 404 hoặc 401
  else hợp lệ
    Ch->>CDB: BEGIN
    Ch->>CDB: INSERT webhook_events
    alt trùng mã sự kiện
      Ch->>CDB: ROLLBACK
      Ch-->>Sim: 202 với trạng thái cũ
    else sự kiện mới
      Ch->>Ch: adapter đổi về Canonical Order
      Ch->>CDB: ghi outbox SubmitOrder, trạng thái Submitted
      Ch->>CDB: COMMIT
      Ch-->>Sim: 202
    end
  end

  Ch->>MQ: đẩy SubmitOrder từ outbox
  MQ->>Core: SubmitOrder
  Core->>Core: luồng giữ hàng
  alt giữ được
    Core->>MQ: OrderReserved
    MQ->>Ch: OrderReserved
    Ch->>CDB: webhook sang Reserved, ghi order_id
    MQ->>Ins: OrderReserved
    Ins->>Ins: đẩy OrderCreated qua SignalR
  else không giữ được
    Core->>MQ: OrderRejected
    MQ->>Ch: OrderRejected
    Ch->>CDB: webhook sang Rejected, ghi lý do
  end
```

## Ba hàng rào chống trùng

| Lớp | Hàng rào | Chặn được |
| --- | --- | --- |
| `channel` | Unique `(tenant_id, channel, event_id)` | Sàn gửi lại cùng webhook |
| `core` inbox | `eventId` của `SubmitOrder` | RabbitMQ giao lại cùng thông điệp |
| `core` đơn | Unique `(tenant_id, channel, external_order_id)` | Hai webhook khác mã sự kiện nhưng cùng một đơn |

## Trường hợp biên

- Payload không đổi được về Canonical Order (thiếu trường, sai kiểu): lưu bản ghi với trạng thái Failed kèm lý do, trả 202, không phát command. Webhook không được trả lỗi 5xx vì payload xấu, để sàn không gửi lại mãi.
- Shop bị tắt: coi như không có shop, trả 404.
- `skuCode` không có ở `core`: `core` phát `OrderRejected` với lý do `UnknownSku`.
- Tenant context: webhook không có JWT. `channel` đặt tenant context theo `ChannelShop` tìm được; command mang `tenantId` đó.
- Bắn tải: `POST /api/channel/simulator/burst` tự sinh và tự gửi các webhook song song vào chính endpoint webhook, mỗi webhook một mã sự kiện và một mã đơn khác nhau.

## Vì sao tách khỏi core

Webhook tới theo đợt dồn dập. `channel` chỉ ghi một dòng và một thông điệp cho mỗi webhook rồi trả lời ngay; hàng đợi đệm phần còn lại. `core` xử lý `SubmitOrder` theo nhịp của mình mà không làm chậm POS và nhân viên đang dùng API của nó.

## Kiểm thử

T01 (bắn 50 đơn vào 1 sản phẩm), T03 (gửi lại cùng webhook), T22 (thông điệp giao lại không xử lý trùng).
