# Thông điệp giữa các service

Service chỉ nói chuyện với nhau bằng thông điệp qua RabbitMQ, phát bằng outbox và nhận bằng inbox. Cách này cho phép transaction nghiệp vụ commit mà không phụ thuộc RabbitMQ đang sống hay chết, đổi lại bên nhận phải chịu được thông điệp tới trễ, tới trùng và tới sai thứ tự.

Danh sách thông điệp và từng trường nằm ở [design/events.md](../design/events.md).

## Mô hình

```mermaid
flowchart LR
  subgraph phat["Service phát"]
    uc["Use case"] -->|cùng transaction| ob[("outbox_messages")]
    ob --> pub["Tiến trình đẩy outbox"]
  end
  pub --> ex(("exchange<br/>oism.events"))
  ex --> q["queue<br/>tên service nhận + tên thông điệp"]
  subgraph nhan["Service nhận"]
    q --> con["Consumer"]
    con -->|cùng transaction| ib[("inbox_messages")]
    con --> eff["Thay đổi nghiệp vụ"]
  end
```

## Phía phát

1. Use case ghi thông điệp vào `outbox_messages` trong cùng transaction với thay đổi nghiệp vụ. Không gọi RabbitMQ bên trong transaction.
2. Một tiến trình nền của service đọc các dòng chưa xử lý bằng `FOR UPDATE SKIP LOCKED`, gửi lên exchange `oism.events` có bật publisher confirm, rồi đánh dấu đã xử lý.
3. Gửi lỗi thì dòng ở lại, lần quét sau gửi tiếp. Hệ quả: một thông điệp có thể được gửi hơn một lần.

## Phía nhận

1. Mỗi cặp service nhận và loại thông điệp có một queue riêng, tên `<service>.<TênThôngĐiệp>`, ví dụ `core.SkuUpserted`.
2. Consumer mở transaction, chèn `eventId` vào `inbox_messages`. Nếu đã có thì bỏ qua và xác nhận thông điệp; nếu chưa thì xử lý, commit, rồi xác nhận.
3. Xử lý lỗi thì thử lại 3 lần có giãn cách. Vẫn lỗi thì chuyển thông điệp sang queue `<queue>.error` và ghi log; không chặn các thông điệp sau.

## Dùng trong một service

Cơ chế nằm ở `Oism.BuildingBlocks/Messaging`; service chỉ khai báo mình phát hay nhận.

| Việc | Làm ở | Cách làm |
| --- | --- | --- |
| Có bảng `outbox_messages` và tiến trình đẩy | Infrastructure | DbContext cài `IHasOutbox`; `AddInfrastructure` gọi `AddOismMessaging<TContext>()`; migration của service tạo bảng |
| Phát một thông điệp | Infrastructure, sau interface `IEventPublisher` của Application | Thêm `OutboxMessage.Create(payload, thời điểm)` vào DbContext trong transaction của use case; `TenantId` do DbContext base gán |
| Có bảng `inbox_messages` và nhận thông điệp | Infrastructure | DbContext cài `IHasInbox`; `AddInfrastructure` gọi `AddOismMessaging<TContext>()` |
| Xử lý một loại thông điệp | Api, thư mục `Consumers` | Lớp kế thừa `EventConsumer<TPayload>`, đăng ký bằng `AddEventConsumer<TConsumer, TPayload>()` ở `Program.cs` |

- Địa chỉ RabbitMQ lấy từ `ConnectionStrings:RabbitMq`, dạng `amqp://user:password@host:5672`. Bỏ trống thì service không gửi và không nhận; thông điệp vẫn nằm trong outbox.
- Exchange `oism.events` là loại topic, bền; routing key là tên thông điệp. Tên service trong tên queue lấy từ schema mặc định của DbContext.
- Phần chung đặt tenant context theo thông điệp, mở transaction, ghi inbox, gọi consumer, lưu thay đổi rồi commit. Vì vậy consumer và handler nó gọi không tự mở transaction và không tự commit.
- Thử lại cách nhau 0,5 giây, 1 giây, 1,5 giây. Một queue xử lý lần lượt từng thông điệp.
- Thông điệp phát ra khi chưa service nào khai báo queue cho loại đó thì broker bỏ đi: bên phát không biết ai nghe. Queue là bền, nên việc này chỉ xảy ra trước lần chạy đầu tiên của bên nhận.

## Bảo đảm và không bảo đảm

| Tính chất | Có hay không | Hệ quả cho người viết consumer |
| --- | --- | --- |
| Không mất thông điệp đã commit | Có | Không cần cơ chế bù |
| Mỗi thông điệp tới đúng một lần | Không, ít nhất một lần | Luôn ghi inbox trước khi xử lý |
| Đúng thứ tự phát | Không | Thông điệp trạng thái mang `version`; bỏ qua bản có `version` nhỏ hơn bản đang giữ |
| Tới ngay | Không | Giao diện và test không giả định dữ liệu tham chiếu có ngay |

## Hai loại thông điệp

| Loại | Ý nghĩa | Số bên nhận | Ví dụ |
| --- | --- | --- | --- |
| Event | Một việc đã xảy ra, bên phát không quan tâm ai nghe | Không giới hạn | `OrderConfirmed`, `StockChanged` |
| Command | Yêu cầu một service làm một việc | Đúng một | `SubmitOrder` gửi cho `core` |

Thông điệp trạng thái (`SkuUpserted`, `BranchUpserted`, `StockChanged`) mang toàn bộ trạng thái hiện tại của đối tượng, không mang phần chênh lệch. Nhờ vậy bên nhận ghi đè theo `version` là đủ, và phát lại không gây sai.

## Phong bì chung

Mọi thông điệp có cùng lớp vỏ; phần riêng nằm trong `payload`.

```json
{
  "eventId": "2f6c1a0e-7c1d-4a55-9d0a-0f4a5f3b9a11",
  "type": "OrderConfirmed",
  "tenantId": "8a0b7c2e-1f3d-4b6a-9c11-5d2e7f809a44",
  "occurredAt": "2026-10-20T03:15:27Z",
  "payload": {}
}
```

## Đổi hợp đồng

- Chỉ được thêm trường mới có giá trị mặc định. Không đổi tên, không xóa, không đổi kiểu trường đang có.
- PR đổi `Oism.Contracts` cần cả dev bên phát và dev bên nhận duyệt, và phải sửa [design/events.md](../design/events.md) trong cùng PR.
- Cần thay đổi không tương thích thì tạo loại thông điệp mới, ví dụ `OrderConfirmedV2`, và cho hai loại chạy song song tới khi mọi bên nhận đã chuyển.

## Hệ quả phải chấp nhận

- SKU hoặc chi nhánh vừa tạo có ở `core` sau một nhịp event. Thao tác dùng tới chúng trước lúc đó nhận lỗi 409 với mã `reference_not_ready`; giao diện tự thử lại.
- Báo cáo, cảnh báo tồn và thông báo realtime trễ theo event, thường dưới vài giây.
- Kết quả giữ hàng của đơn online về tới `channel` bằng event, không nằm trong phản hồi của webhook.

## Kiểm thử

- T22: phát lại cùng một thông điệp, bên nhận không xử lý lần hai.
- T23: tắt RabbitMQ lúc commit, giao dịch vẫn thành công và thông điệp được gửi bù khi RabbitMQ chạy lại.
