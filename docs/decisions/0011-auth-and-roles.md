# ADR-0011: JWT ký RS256, refresh token xoay vòng, mỗi người dùng thuộc một tenant

- Trạng thái: Đề xuất, chờ giảng viên xác nhận
- Gộp từ: D-15
- Ngày: 2026-10-03

## Bối cảnh

NFR-SEC-01 nêu BCrypt hoặc Argon2, access token 60 phút và "refresh token được bảo vệ an toàn", nhưng không nói thời hạn refresh token, cách thu hồi, hay đăng xuất nghĩa là gì với token còn hạn. FR-AUTH-03 nêu ba vai trò mà không phân quyền chi tiết. Đề không nói một người dùng có thuộc nhiều tenant hay nhiều chi nhánh được không, và không phân biệt chi nhánh với kho.

## Quyết định

- Mật khẩu băm BCrypt.
- Access token là JWT ký RS256, sống 60 phút, mang `sub`, `tenant_id`, `role`, và `branch_id` nếu có.
- Refresh token sống 7 ngày, lưu dạng băm, xoay vòng mỗi lần dùng, bị thu hồi khi đăng xuất.
- Gateway và các service kiểm JWT bằng khóa công khai trong cấu hình; không gọi `identity`.
- Mỗi người dùng thuộc đúng một tenant. Email và số điện thoại unique trên toàn hệ thống.
- Một thực thể `Branch` có `Type` là Store hoặc Warehouse; không tách kho thành thực thể riêng.
- Cashier gắn với một chi nhánh. Staff thấy mọi chi nhánh của tenant.
- Báo cáo lợi nhuận và giá vốn chỉ Owner xem.

Bảng quyền đầy đủ ở [architecture/security.md](../architecture/security.md).

## Hệ quả

- Đăng nhập không cần chọn tenant: tìm được người dùng là biết tenant.
- Đăng xuất không vô hiệu access token đang còn hạn; rủi ro tối đa là 60 phút.
- Không có quản trị viên toàn nền tảng; tenant mới tạo qua trang đăng ký công khai.

## Phương án đã loại

- Ký đối xứng HS256 với khóa chung: mọi service đều giữ khóa ký được token, lộ một nơi là giả mạo được toàn hệ thống.
- Người dùng thuộc nhiều tenant: thêm bước chọn tenant và bảng thành viên; đề không yêu cầu.
- Danh sách đen access token để đăng xuất có hiệu lực ngay: cần kho dùng chung giữa các service.

## Nếu bị đổi

Yêu cầu đăng xuất có hiệu lực ngay: rút thời hạn access token xuống vài phút hoặc thêm danh sách thu hồi ở gateway.
