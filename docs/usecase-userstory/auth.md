# Use case: tenant và xác thực

Bốn use case của service `identity`. Thiết kế: [data model](../design/data-model/identity.md), [API](../design/api/identity.md), [bảo mật](../architecture/security.md).

## UC-AUTH-01 Đăng ký tenant

- Actor: người đăng ký, sau đó là Owner
- Yêu cầu: FR-AUTH-01, NFR-TENANT-01
- API: `POST /api/identity/tenants`

Là chủ cửa hàng, tôi muốn đăng ký một không gian riêng cho doanh nghiệp của mình, để dữ liệu của tôi tách biệt với doanh nghiệp khác.

Tiêu chí chấp nhận:

1. Cho thông tin hợp lệ, khi đăng ký, thì hệ thống tạo một tenant có `TenantId` mới và một người dùng vai trò Owner thuộc tenant đó.
2. Cho email hoặc số điện thoại đã có trong hệ thống, khi đăng ký, thì trả 409 và không tạo gì.
3. Cho hai tenant A và B, khi Owner của A đọc bất kỳ danh sách nào, thì không có bản ghi nào của B (T14).
4. Mật khẩu được lưu dạng băm BCrypt; không API nào trả về mật khẩu hay giá trị băm.

## UC-AUTH-02 Đăng nhập, làm mới phiên, đăng xuất

- Actor: Owner, Staff, Cashier
- Yêu cầu: FR-AUTH-02, NFR-SEC-01
- API: `POST /api/identity/auth/login`, `/auth/refresh`, `/auth/logout`

Là người dùng, tôi muốn đăng nhập bằng email hoặc số điện thoại, để làm việc trong đúng tenant và đúng quyền của mình.

Tiêu chí chấp nhận:

1. Cho email hoặc số điện thoại đúng và mật khẩu đúng, khi đăng nhập, thì nhận access token sống 60 phút mang `tenant_id`, `sub`, `role`, và một refresh token.
2. Cho mật khẩu sai hoặc tài khoản không tồn tại, khi đăng nhập, thì trả 401 với cùng một thông báo cho cả hai trường hợp.
3. Cho refresh token còn hạn, khi làm mới, thì nhận cặp token mới và refresh token cũ hết dùng được.
4. Cho refresh token đã bị thay, khi dùng lại, thì trả 401 và mọi refresh token của người dùng đó bị thu hồi.
5. Cho người dùng đang đăng nhập, khi đăng xuất, thì refresh token bị thu hồi.
6. Cho người dùng đã bị vô hiệu hóa, khi đăng nhập hoặc làm mới, thì trả 401.

## UC-AUTH-03 Quản lý người dùng và vai trò

- Actor: Owner
- Yêu cầu: FR-AUTH-03
- API: `GET, POST, PUT /api/identity/users`

Là Owner, tôi muốn tạo tài khoản cho nhân viên với đúng vai trò, để mỗi người chỉ làm được phần việc của họ.

Tiêu chí chấp nhận:

1. Cho Owner, khi tạo người dùng vai trò Staff hoặc Cashier, thì người dùng mới thuộc tenant của Owner; Cashier bắt buộc gắn với một chi nhánh.
2. Cho Staff hoặc Cashier, khi gọi API quản lý người dùng, thì trả 403.
3. Cho Cashier, khi gọi bất kỳ API nào ngoài nhóm POS và thông báo, thì trả 403.
4. Cho Staff, khi gọi API báo cáo lợi nhuận hoặc API đặt giá, thì trả 403.
5. Cho một endpoint chưa khai báo quyền, khi bất kỳ ai gọi, thì bị từ chối.

Bảng quyền đầy đủ ở [security.md](../architecture/security.md).

## UC-AUTH-04 Quản lý chi nhánh

- Actor: Owner
- Yêu cầu: FR-AUTH-04
- API: `GET, POST, PUT /api/identity/branches`, `PATCH /api/identity/branches/{id}/active`
- Event: `BranchUpserted`

Là Owner, tôi muốn khai báo các cửa hàng và kho của mình, để tồn kho và đơn hàng được quản lý theo từng nơi.

Tiêu chí chấp nhận:

1. Cho mã chi nhánh chưa dùng trong tenant, khi tạo chi nhánh loại Store hoặc Warehouse, thì chi nhánh được tạo ở trạng thái đang hoạt động.
2. Cho mã chi nhánh đã dùng trong cùng tenant, khi tạo, thì trả 409; cùng mã ở tenant khác vẫn tạo được.
3. Cho chi nhánh đang hoạt động, khi tắt, thì không tạo được đơn, phiếu nhập hay phiếu chuyển kho mới cho chi nhánh đó; dữ liệu cũ giữ nguyên.
4. Cho bất kỳ thay đổi nào về chi nhánh, khi lưu xong, thì `BranchUpserted` được phát và `core` có bản sao chi nhánh sau một nhịp event.
