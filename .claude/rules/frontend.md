---
paths:
  - "frontend/**"
---

# Frontend

Nguồn chuẩn: `docs/conventions/frontend.md` cho quy ước, `docs/design/ui/admin.md` và `docs/design/ui/pos.md` cho màn hình, `docs/design/api/` cho API.

- TypeScript `strict`; không dùng `any`.
- Ba lớp: `pages` gọi `features`, `features` gọi `frontend/shared`. `admin` và `pos` không import của nhau.
- Mọi lời gọi API đi qua `frontend/shared/src/api`; component không tự gọi `fetch`. Chỉ gọi gateway, không gọi thẳng cổng của service.
- Kiểu dữ liệu của API ở `frontend/shared/src/types` phải khớp `docs/design/api/`.
- Xử lý lỗi theo trường `code` của ProblemDetails, không theo chuỗi thông báo. `reference_not_ready` thì tự thử lại; `insufficient_stock` thì hiện SKU và số còn lại từ `details`.
- Route, vai trò và API của từng màn hình theo bảng ở `docs/design/ui/`. Thêm màn hình thì thêm vào bảng đó trước.
- Ẩn nút theo vai trò chỉ để gọn giao diện; quyền do backend kiểm. Không tự tính những trường backend không trả (giá vốn, lợi nhuận).
- Giao diện không quyết định nghiệp vụ. Kiểm tồn ở POS chỉ để báo sớm.
- POS: ô tìm luôn giữ focus; mỗi lần bấm thanh toán sinh một `Idempotency-Key` và dùng lại khóa đó khi thử lại; chỉ in sau khi backend trả đơn đã lưu; service worker chỉ cache app shell.
- Chuỗi hiển thị bằng tiếng Việt. Tiền theo định dạng Việt Nam, không phần thập phân; thời gian hiển thị theo giờ Việt Nam.
- Dùng một thư viện component đã chọn ở `docs/conventions/frontend.md`; không thêm thư viện component khác.
