# ADR-0006: Kênh sàn chỉ là simulator; ba hàng rào chống xử lý trùng

- Trạng thái: Đề xuất, chờ giảng viên xác nhận
- Gộp từ: D-09, D-10
- Ngày: 2026-10-03

## Bối cảnh

FR-ORD-01 nêu Shopee và TikTok; FR-SIM-01 thêm Lazada. Đề mô tả kết nối sàn ở mức webhook giả lập, không nói tới API thật, cấp quyền tài khoản sàn hay đẩy tồn ngược về sàn. Đề cũng không nói cách xử lý khi webhook gửi lại, thu ngân bấm thanh toán hai lần hay job chạy lại.

## Quyết định

- Không gọi API thật của sàn nào. `channel` nhận webhook từ simulator và có công cụ bắn tải.
- Cả ba kênh Shopee, TikTok, Lazada đi cùng một đường: adapter riêng của từng kênh đổi payload về Canonical Order, rồi gửi command `SubmitOrder` cho `core`.
- Ba hàng rào chống trùng:
  1. `channel`: unique `(tenant_id, channel, event_id)` trên `webhook_events`.
  2. `core`: unique `(tenant_id, channel, external_order_id)` trên `orders`.
  3. POS: header `Idempotency-Key`, unique `(tenant_id, idempotency_key)` trên `orders`.
- Mọi consumer ghi `eventId` vào inbox trước khi xử lý.

## Hệ quả

- OISM từ chối giữ hàng khi hết tồn, nhưng trong thực tế sàn có thể đã nhận đơn. Việc chống bán vượt chỉ được cam kết bên trong OISM.
- Webhook gửi lại nhận 202 với trạng thái của lần xử lý đầu; không có đơn, phần giữ hàng hay ledger nào được tạo thêm.
- Phản hồi của webhook không chứa kết quả giữ hàng; kết quả về sau bằng event `OrderReserved` hoặc `OrderRejected`.

## Phương án đã loại

- Kết nối API thật của sàn: cần tài khoản đối tác, xử lý giới hạn API và đồng bộ tồn ngược; vượt phạm vi 5 tuần.
- Chỉ chống trùng ở ứng dụng bằng cách kiểm trước khi ghi: hai request đồng thời cùng qua được bước kiểm.

## Nếu bị đổi

Yêu cầu kết nối thật: thêm client cho từng sàn trong `channel`, thêm luồng đẩy tồn từ `StockChanged`, thêm quy tắc xử lý đơn bị từ chối sau khi sàn đã nhận.
