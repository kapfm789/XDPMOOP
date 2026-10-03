---
paths:
  - "backend/**/*.Api/**"
  - "backend/gateway/**"
---

# Lớp Api và gateway

Api nhận request, gọi một handler và trả kết quả. Nguồn chuẩn: `docs/design/api/`, `docs/architecture/security.md`.

- Endpoint, vai trò, dữ liệu vào ra và mã lỗi phải khớp `docs/design/api/<service>.md`. Thêm hoặc đổi endpoint thì sửa file đó trong cùng thay đổi.
- Route trong service không mang tiền tố `/api/<service>`; gateway đã bỏ tiền tố đó.
- Mỗi action khai báo policy theo vai trò, khớp bảng quyền ở `docs/architecture/security.md`. Endpoint công khai phải ghi rõ `AllowAnonymous` và chỉ gồm: đăng ký tenant, đăng nhập, làm mới token, webhook, health check.
- Controller không chứa nghiệp vụ, không có `try/catch`, không dùng `DbContext`. Lỗi do middleware chung đổi sang ProblemDetails kèm `code`.
- Không nhận `tenantId` từ URL hay body. Tenant lấy từ token.
- Trường chỉ Owner được xem (giá vốn, lợi nhuận) bị loại khỏi phản hồi cho vai trò khác ở backend, không ở giao diện.
- Consumer đặt trong thư mục `Consumers`, kế thừa consumer base của `Oism.BuildingBlocks` để ghi inbox trước khi xử lý, rồi gọi một handler.
- `Program.cs` chỉ đăng ký DI và cấu hình pipeline: xác thực, tenant middleware, ProblemDetails, Swagger, health check.
- Gateway chỉ định tuyến, kiểm JWT, CORS, rate limit và kết thúc TLS. Không có nghiệp vụ và không có database. Cổng và tiền tố theo `docs/architecture/context-and-containers.md`.
