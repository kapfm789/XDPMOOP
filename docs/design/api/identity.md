# API: identity

Tiền tố `/api/identity`. Use case ở [auth.md](../../usecase-userstory/auth.md); bảng ở [data-model/identity.md](../data-model/identity.md). Quy ước chung ở [README.md](README.md).

## Tenant và phiên

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/identity/tenants` | Công khai | `tenantName`, `ownerName`, `email?`, `phone?`, `password` | 201: `tenantId`, `userId` | 409 `duplicate` |
| POST | `/api/identity/auth/login` | Công khai | `identifier` (email hoặc số điện thoại), `password` | 200: `accessToken`, `expiresIn`, `refreshToken`, `user` | 401 |
| POST | `/api/identity/auth/refresh` | Công khai | `refreshToken` | 200: như đăng nhập | 401 |
| POST | `/api/identity/auth/logout` | Mọi vai trò | `refreshToken` | 204 | |
| GET | `/api/identity/me` | Mọi vai trò | | `id`, `fullName`, `role`, `branchId?`, `tenantName` | |

`user` trong phản hồi đăng nhập gồm `id`, `fullName`, `role`, `branchId?`. Đăng ký tenant yêu cầu ít nhất một trong `email`, `phone`; mật khẩu tối thiểu 8 ký tự.

- `expiresIn` tính bằng giây (3600). `refreshToken` là chuỗi ngẫu nhiên; mỗi lần làm mới trả một chuỗi mới và chuỗi cũ hết dùng được.
- Email được cắt khoảng trắng và đổi sang chữ thường trước khi lưu và trước khi so khớp. Số điện thoại gồm 8 đến 15 chữ số, có thể bắt đầu bằng `+`.
- Đăng nhập sai mật khẩu, tài khoản không tồn tại, tài khoản bị vô hiệu hóa, refresh token lạ, hết hạn hoặc đã thu hồi đều trả 401 `unauthenticated` với cùng một nội dung.
- Đăng xuất với refresh token không tồn tại hoặc không phải của người gọi vẫn trả 204 và không thu hồi gì.

## Người dùng

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/identity/users` | Owner | `page`, `pageSize` | Danh sách người dùng | |
| POST | `/api/identity/users` | Owner | `fullName`, `email?`, `phone?`, `password`, `role`, `branchId?` | 201: người dùng | 409 `duplicate`; 400 khi Cashier thiếu `branchId` |
| PUT | `/api/identity/users/{id}` | Owner | `fullName`, `role`, `branchId?`, `isActive` | 200: người dùng | 400 khi Cashier thiếu `branchId`; 400 khi `id` là tài khoản Owner |

`role` nhận `Staff` hoặc `Cashier`. Owner chỉ sinh ra khi đăng ký tenant, và tài khoản Owner không sửa được qua `PUT /users/{id}`.

- Người dùng trả về gồm `id`, `fullName`, `email?`, `phone?`, `role`, `branchId?`, `isActive`, `createdAt`; không bao giờ có mật khẩu hay giá trị băm.
- Danh sách xếp mới nhất trước và trả theo khuôn phân trang chung; `page` nhỏ hơn 1 hoặc `pageSize` ngoài khoảng 1 đến 100 trả 400.
- Việc kiểm `branchId` có thật trong tenant được thêm ở task W1-05, khi có bảng `branches`.

## Chi nhánh

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/identity/branches` | Owner, Staff | `isActive?` | Danh sách chi nhánh | |
| POST | `/api/identity/branches` | Owner | `code`, `name`, `type`, `address?` | 201: chi nhánh | 409 `duplicate` |
| PUT | `/api/identity/branches/{id}` | Owner | `name`, `type`, `address?` | 200: chi nhánh | |
| PATCH | `/api/identity/branches/{id}/active` | Owner | `isActive` | 200: chi nhánh | |

Mọi thay đổi chi nhánh phát `BranchUpserted` ([events.md](../events.md)).
