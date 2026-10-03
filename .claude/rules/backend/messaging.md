---
paths:
  - "backend/shared/**"
  - "backend/**/Consumers/**"
---

# Thông điệp giữa các service

Service chỉ nói chuyện với nhau bằng thông điệp qua outbox và inbox. Nguồn chuẩn: `docs/architecture/messaging.md` cho cơ chế, `docs/design/events.md` cho từng trường.

- Tên, trường và kiểu của mỗi thông điệp trong `Oism.Contracts` phải khớp `docs/design/events.md`. Đổi hợp đồng thì sửa file đó trong cùng thay đổi.
- Hợp đồng chỉ được thêm trường có giá trị mặc định. Không đổi tên, không xóa, không đổi kiểu. Cần thay đổi không tương thích thì tạo loại thông điệp mới.
- Mọi thông điệp đi trong phong bì chung có `eventId`, `type`, `tenantId`, `occurredAt`.
- Bên phát ghi `outbox_messages` trong cùng transaction với thay đổi nghiệp vụ. Tiến trình đẩy outbox dùng `FOR UPDATE SKIP LOCKED` và publisher confirm.
- Consumer chèn `eventId` vào `inbox_messages` trước khi xử lý, trong cùng transaction với thay đổi nó tạo ra. Đã có thì bỏ qua và xác nhận thông điệp.
- Consumer đặt tenant context từ `tenantId` của thông điệp trước khi chạm database.
- Thông điệp trạng thái (`SkuUpserted`, `BranchUpserted`, `StockChanged`) được ghi đè theo `version`; bản có `version` nhỏ hơn hoặc bằng bản đang giữ thì bỏ qua.
- Consumer phải chịu được thông điệp tới trùng, tới trễ và tới sai thứ tự. Không giả định dữ liệu tham chiếu đã có.
- `Oism.BuildingBlocks` không chứa nghiệp vụ của bất kỳ service nào.
- Kiểm thử: T22 (giao lại không xử lý trùng) và T23 (RabbitMQ tắt lúc commit) ở `docs/testing/scenarios.md`.
