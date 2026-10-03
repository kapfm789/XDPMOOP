# Bảo mật

Người dùng đăng nhập ở `identity` và nhận JWT; gateway và từng service tự kiểm JWT đó bằng khóa công khai, không service nào gọi ngược về `identity`. Quyền được kiểm ở backend theo vai trò; việc ẩn nút ở giao diện chỉ là tiện dụng.

Quyết định liên quan: [ADR-0011](../decisions/0011-auth-and-roles.md).

## Xác thực

| Hạng mục | Thiết kế |
| --- | --- |
| Đăng nhập | Email hoặc số điện thoại kèm mật khẩu (FR-AUTH-02) |
| Băm mật khẩu | BCrypt, work factor 12 (NFR-SEC-01) |
| Access token | JWT ký RS256, sống 60 phút |
| Claim | `sub` (UserId), `tenant_id`, `role`, `branch_id` khi người dùng gắn với một chi nhánh |
| Refresh token | Chuỗi ngẫu nhiên, sống 7 ngày, lưu dạng băm, xoay vòng mỗi lần dùng |
| Đăng xuất | Thu hồi refresh token; access token còn hiệu lực tới khi hết hạn |
| Dùng lại refresh token đã xoay | Thu hồi cả chuỗi token của người dùng đó |
| Kiểm JWT | Gateway kiểm trước; service kiểm lại bằng khóa công khai lấy từ cấu hình |
| Giới hạn thử đăng nhập | Rate limit ở gateway cho `/api/identity/auth/login` |

`identity` giữ khóa bí mật để ký. Khóa công khai được cấp cho gateway và các service qua biến môi trường `Jwt__PublicKey`, dạng SubjectPublicKeyInfo mã hóa base64 trên một dòng. Thiếu khóa thì không token nào hợp lệ.

## Phân quyền theo vai trò

Owner có toàn quyền trong tenant của mình (FR-AUTH-03). Bảng dưới là nguồn chuẩn; cột vai trò ở [design/api/](../design/api/) phải khớp bảng này.

| Năng lực | Owner | Staff | Cashier |
| --- | --- | --- | --- |
| Quản lý người dùng và chi nhánh | Có | Không | Không |
| Xem danh sách chi nhánh | Có | Có | Không |
| Danh mục, thương hiệu, sản phẩm, SKU, mã vạch | Có | Có | Không |
| Đặt giá lẻ, giá sỉ | Có | Không | Không |
| Phiếu nhập, chuyển kho, kiểm kê, ngưỡng tồn | Có | Có | Không |
| Xem tồn và ledger | Có | Có | Không |
| Đơn hàng: xem, tạo thủ công, duyệt, hoàn tất, hủy | Có | Có | Không |
| POS: tìm SKU kèm tồn | Có | Có | Có |
| POS: thanh toán | Có | Không | Có |
| Báo cáo lợi nhuận gộp, giá vốn | Có | Không | Không |
| Báo cáo giá trị tồn, bán chạy, bán chậm, cảnh báo tồn | Có | Có | Không |
| Đề xuất nhập hàng, chạy dự báo | Có | Không | Không |
| Cấu hình shop của sàn, simulator, xem webhook | Có | Không | Không |
| Nhận thông báo realtime | Có | Có | Có |

Cashier gắn với một chi nhánh qua claim `branch_id` và chỉ thanh toán được ở chi nhánh đó.

## Cách kiểm quyền trong code

- Mỗi endpoint khai báo vai trò bằng policy của ASP.NET Core; policy định nghĩa một lần trong `Oism.BuildingBlocks/Auth`.
- Endpoint không khai báo policy thì mặc định từ chối. Endpoint công khai phải ghi rõ `AllowAnonymous`.
- Endpoint công khai chỉ gồm: đăng ký tenant, đăng nhập, làm mới token, webhook của sàn, health check.
- Lỗi thiếu quyền trả 403. Dữ liệu của tenant khác trả 404 ([multi-tenancy.md](multi-tenancy.md)).

## Webhook

Webhook không mang JWT. `channel` xác định tenant qua `ChannelShop` và kiểm chữ ký HMAC giả lập trong header `X-Signature`, tính trên body bằng khóa bí mật của shop. Chữ ký sai trả 401 và không lưu gì.

## Truyền tải và bí mật

- HTTPS với TLS 1.3 kết thúc ở gateway. Các service phía sau nói HTTP trong mạng nội bộ của Compose.
- Không đưa bí mật vào repo. Cấu hình mẫu nằm ở `.env.example`; giá trị thật nằm ở `.env` đã được git bỏ qua.
- Không ghi mật khẩu, token hay khóa vào log.
- CORS ở gateway chỉ cho phép origin của `admin` và `pos`.

## Dấu vết

- Mỗi dòng ledger ghi `created_by` và chứng từ nguồn; job hệ thống để trống `created_by`.
- Mỗi request mang một correlation id do gateway sinh, đi theo log của mọi service và theo event.

## Phần đề chưa nói và nhóm tự chốt

| Điểm | Lựa chọn |
| --- | --- |
| Thời hạn refresh token | 7 ngày |
| Nghĩa của đăng xuất với access token còn hạn | Không thu hồi; chờ hết 60 phút |
| Quyền theo chi nhánh của Staff | Staff thấy mọi chi nhánh trong tenant |
| Người dùng thuộc nhiều tenant | Không hỗ trợ; mỗi người dùng thuộc một tenant |
