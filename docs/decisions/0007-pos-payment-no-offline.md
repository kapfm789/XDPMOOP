# ADR-0007: Thanh toán QR do thu ngân xác nhận tay; POS không bán offline

- Trạng thái: Đề xuất, chờ giảng viên xác nhận
- Gộp từ: D-11
- Ngày: 2026-10-03

## Bối cảnh

FR-POS-03 nêu tiền mặt và chuyển khoản QR nhưng không nói hệ thống tự nhận kết quả thanh toán hay thu ngân xác nhận. Đề gọi POS là PWA nhưng không nhắc chế độ offline. FR-POS-04 chỉ nói các thao tác dữ liệu phải nguyên tử.

## Quyết định

- Tiền mặt và QR đều do thu ngân xác nhận tay trước khi bấm thanh toán. Với QR, POS hiển thị mã chuyển khoản của cửa hàng; hệ thống không kết nối ngân hàng.
- `Payment` ghi phương thức, số tiền và người xác nhận. Không có trạng thái "chờ thanh toán".
- POS không bán khi mất mạng. PWA nghĩa là cài được lên màn hình chính và cache app shell để mở nhanh.
- In hóa đơn qua hộp thoại in của trình duyệt, chỉ sau khi backend trả đơn đã lưu.

## Hệ quả

- Checkout POS là một transaction database thuần túy; không có giao dịch tiền bên ngoài nào cần bù trừ.
- Mất mạng thì không bán được. Đổi lại, không phải chia tồn cho thiết bị offline và không phải xử lý xung đột khi đồng bộ.
- In lỗi không tạo đơn mới: thu ngân in lại từ đơn đã có.

## Phương án đã loại

- Tích hợp cổng thanh toán hoặc đối soát QR tự động: phải xử lý trường hợp đã nhận tiền nhưng ghi đơn lỗi và ngược lại; transaction database không hoàn tác được giao dịch tiền bên ngoài.
- Bán offline: cần chính sách phân bổ tồn cho từng thiết bị và quy tắc xử lý xung đột.

## Nếu bị đổi

- QR tự động: thêm trạng thái thanh toán tách khỏi trạng thái đơn, thêm webhook của cổng thanh toán và quy tắc hoàn tiền.
- Bán offline: thêm kho ảo cho từng thiết bị và luồng đồng bộ; đây là phần mở rộng lớn.
