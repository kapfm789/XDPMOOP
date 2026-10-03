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

## Người dùng

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/identity/users` | Owner | `page`, `pageSize` | Danh sách người dùng | |
| POST | `/api/identity/users` | Owner | `fullName`, `email?`, `phone?`, `password`, `role`, `branchId?` | 201: người dùng | 409 `duplicate`; 400 khi Cashier thiếu `branchId` |
| PUT | `/api/identity/users/{id}` | Owner | `fullName`, `role`, `branchId?`, `isActive` | 200: người dùng | |

`role` nhận `Staff` hoặc `Cashier`. Owner chỉ sinh ra khi đăng ký tenant.

## Chi nhánh

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/identity/branches` | Owner, Staff | `isActive?` | Danh sách chi nhánh | |
| POST | `/api/identity/branches` | Owner | `code`, `name`, `type`, `address?` | 201: chi nhánh | 409 `duplicate` |
| PUT | `/api/identity/branches/{id}` | Owner | `name`, `type`, `address?` | 200: chi nhánh | |
| PATCH | `/api/identity/branches/{id}/active` | Owner | `isActive` | 200: chi nhánh | |

Mọi thay đổi chi nhánh phát `BranchUpserted` ([events.md](../events.md)).
