# backend/services/identity — kế hoạch folder

Quản lý tenant, người dùng, đăng nhập, phân quyền và chi nhánh (FR-AUTH-01..04). Owner: Dev A. Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Phân lớp

```text
identity/
├─ src/
│  ├─ Oism.Identity.Domain/           Tenant, User, Role, RefreshToken, Branch
│  ├─ Oism.Identity.Application/      RegisterTenant, Login, RefreshToken, Logout, UpsertBranch, SetBranchActive
│  ├─ Oism.Identity.Infrastructure/   IdentityDbContext, migration, BCrypt, phát JWT, outbox
│  └─ Oism.Identity.Api/              AuthController, TenantsController, UsersController, BranchesController
└─ tests/
   ├─ Oism.Identity.UnitTests/
   └─ Oism.Identity.IntegrationTests/
```

Phụ thuộc chỉ hướng vào trong: Api → Application → Domain; Infrastructure cài đặt interface của Application.

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 2 | W1-04 | identity: đăng ký tenant, login bằng email hoặc số điện thoại, JWT (TenantId, UserId, Role), refresh, logout, RBAC | A | FR-AUTH-01..03, NFR-SEC-01 | JWT đủ claim; Cashier gọi API quản trị nhận 403 |
| 2 | W1-05 | identity: CRUD chi nhánh, bật/tắt, phát `BranchUpserted` qua outbox | A | FR-AUTH-04 | Tạo chi nhánh xong, `core` có BranchRef |
| 2 | W1-12 | Rà ERD ở `docs/design/data-model/` theo migration thật; test cách ly tenant cho identity và catalog | A (ERD), B (test) | NFR-TENANT-01 | ERD khớp migration; tenant A gọi ID của tenant B nhận 404 |
| 9 | W5-02 | Bộ test cách ly tenant toàn hệ thống: API, event, SignalR, job | A | NFR-TENANT-01 | Mọi ca dùng ID tenant khác nhận 404 hoặc 403 |
| 10 | W5-12 | identity, admin, pos: tài khoản nhận chuyển khoản theo chi nhánh; POS hiện mã VietQR của chi nhánh và in phiếu thanh toán tạm tính kèm mã | C | FR-AUTH-04, FR-POS-03 | Owner khai tài khoản nhận cho chi nhánh; POS ở chi nhánh đó hiện và in được mã VietQR quét được bằng app ngân hàng; in phiếu thanh toán không tạo đơn |

## Sở hữu

- Schema `identity` của database `oism`: Tenant, User, RefreshToken, Branch (mục 5 của kế hoạch tổng).
- API: `/tenants`, `/auth/login`, `/auth/refresh`, `/auth/logout`, `/users`, `/branches` (mục 8).
- Event phát: `BranchUpserted` (mục 7).
- Kiểm thử: T14; điều kiện xong của W1-04 và W1-05.

## Quy tắc riêng

- JWT chứa `TenantId`, `UserId`, `Role`; access token 60 phút, refresh token 7 ngày, xoay vòng, lưu dạng băm (D-15).
- Email và số điện thoại unique trên toàn hệ thống, vì đăng nhập chưa biết tenant.
