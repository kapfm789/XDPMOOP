---
name: add-event
description: "Thêm một thông điệp mới giữa các service của OISM, hoặc thêm trường vào một thông điệp đã có, sao cho hợp đồng, bên phát, bên nhận, tài liệu và test khớp nhau. Dùng skill này khi người dùng nói 'thêm event', 'phát event', 'thêm consumer', 'service này cần dữ liệu của service kia', 'thêm trường vào event', hoặc khi một thay đổi cần đưa dữ liệu từ service này sang service khác. Luôn dùng skill này thay vì gọi HTTP giữa các service hay đọc schema của service khác."
argument-hint: "[tên thông điệp, ví dụ OrderConfirmed]"
---

# Thêm hoặc đổi một thông điệp

Thông điệp: $ARGUMENTS

Trong OISM, service chỉ nói chuyện với nhau bằng thông điệp qua outbox và inbox. Một thông điệp là hợp đồng giữa hai dev sở hữu hai service, nên thay đổi nó phải đi cùng tài liệu và test ở cả hai phía. Cơ chế nằm ở `docs/architecture/messaging.md`; danh sách và từng trường nằm ở `docs/design/events.md`.

## 1. Xác định có thật sự cần thông điệp mới không

- Dữ liệu cần dùng đã có trong một thông điệp hiện có chưa. Có thì chỉ cần thêm bên nhận.
- Chỉ thiếu một trường thì thêm trường vào thông điệp hiện có, không tạo thông điệp mới.
- Nếu hai phần cần commit cùng nhau thì chúng thuộc cùng một service; thông điệp không giải quyết được việc đó. Dừng lại và báo người dùng, vì đây là câu hỏi về ranh giới service.

## 2. Chọn loại

| Loại | Dùng khi | Quy tắc |
| --- | --- | --- |
| Event | Báo một việc đã xảy ra | Tên là danh từ kèm quá khứ phân từ, ví dụ `OrderConfirmed` |
| Event trạng thái | Chép dữ liệu tham chiếu sang service khác | Mang toàn bộ trạng thái hiện tại và một trường `version`; bên nhận ghi đè theo `version` |
| Command | Yêu cầu đúng một service làm một việc | Tên là động từ kèm danh từ, ví dụ `SubmitOrder` |

## 3. Sửa tài liệu trước

Sửa `docs/design/events.md`: bảng tổng (loại, bên phát, bên nhận, khi nào) và bảng trường của thông điệp. Nếu thông điệp đổi `sku_refs`, `stock_snapshots` hay bảng bản sao nào khác, sửa cả `docs/design/data-model/`.

Quy tắc đổi hợp đồng: chỉ thêm trường có giá trị mặc định. Không đổi tên, không xóa, không đổi kiểu trường đang có. Cần thay đổi không tương thích thì tạo loại mới (ví dụ `OrderConfirmedV2`) và để hai loại chạy song song.

## 4. Hợp đồng

Thêm hoặc sửa kiểu trong `backend/shared/Oism.Contracts`. Kiểu là record bất biến; không chứa logic. `eventId`, `tenantId`, `occurredAt` thuộc phong bì chung, không khai báo lại trong payload.

## 5. Bên phát

- Trong handler của use case sinh ra thông điệp, gọi `IEventPublisher.Enqueue(...)` bên trong transaction, trước khi commit.
- Không gửi RabbitMQ trực tiếp và không gửi sau khi commit bằng code riêng; tiến trình đẩy outbox lo việc đó.
- Với event trạng thái, tăng `version` của bản ghi trong cùng transaction.

## 6. Bên nhận

- Tạo consumer trong `Oism.<Service>.Api/Consumers`, kế thừa consumer base của `Oism.BuildingBlocks`. Consumer base ghi inbox, đặt tenant context và mở transaction.
- Consumer chỉ gọi một handler ở Application.
- Handler phải chịu được ba tình huống: thông điệp tới hai lần, tới trễ, tới sai thứ tự. Với event trạng thái, bỏ qua bản có `version` nhỏ hơn hoặc bằng bản đang giữ.
- Khai báo queue `<service>.<TênThôngĐiệp>` gắn vào exchange `oism.events`.

## 7. Test

| Test | Kiểm |
| --- | --- |
| Bên phát, tích hợp | Use case commit thì có đúng một dòng outbox với payload đúng; use case rollback thì không có dòng nào |
| Bên nhận, tích hợp | Xử lý đúng; giao lại cùng `eventId` không tác động lần hai (kịch bản T22) |
| Bên nhận, event trạng thái | Bản `version` cũ tới sau bản mới thì bị bỏ qua |
| Tenant | Thay đổi rơi vào đúng tenant ghi trong thông điệp |

## 8. Báo cáo

Nêu: thông điệp đã thêm hoặc đổi, bên phát và use case phát, từng bên nhận và việc nó làm, tài liệu đã sửa, test đã thêm, lệnh đã chạy. Nhắc người dùng rằng PR đổi `Oism.Contracts` cần cả dev bên phát và dev bên nhận duyệt. Không commit.
