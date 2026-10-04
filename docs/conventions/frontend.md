# Quy ước frontend

`admin` và `pos` là hai ứng dụng React trong cùng một workspace npm, dùng chung gói `shared`. Giao diện không quyết định nghiệp vụ: nó hiển thị, thu dữ liệu và gọi API. Màn hình và route ở [design/ui/](../design/ui/admin.md); quyết định về cấu trúc ở [ADR-0012](../decisions/0012-frontend-workspaces.md).

## Nền

- React với Vite và TypeScript ở chế độ `strict`. Không dùng `any`; kiểu chưa biết thì dùng `unknown` rồi thu hẹp.
- Component là hàm; state cục bộ bằng hook.
- Dữ liệu từ server lấy qua TanStack Query; không tự viết cache.
- Định tuyến bằng React Router.
- Một thư viện component cho cả hai ứng dụng: Ant Design. Không trộn thêm thư viện component khác.
- Chuỗi hiển thị viết tiếng Việt trực tiếp trong component; dự án không dùng khung đa ngôn ngữ.

Các lựa chọn trên đã chốt ở task W1-09 với các bản: React 19, Vite 8, TypeScript 7, TanStack Query 5, React Router 7 (gói `react-router-dom`), Ant Design 6, Vitest 5. Đổi thì sửa file này trước.

## Workspace và lệnh

`frontend/package.json` khai báo ba gói `@oism/shared`, `@oism/admin`, `@oism/pos`. `shared` không có bước build: hai ứng dụng import thẳng mã TypeScript của nó qua `@oism/shared`, và chỉ dùng những gì `shared/src/index.ts` xuất ra.

| Lệnh, chạy với `npm --prefix frontend` | Việc |
| --- | --- |
| `ci` | Cài package theo `package-lock.json` |
| `run dev:admin`, `run dev:pos` | Chạy `admin` ở cổng 5173, `pos` ở cổng 5174 |
| `run typecheck` | Kiểm kiểu cả ba gói |
| `test` | Chạy test Vitest của `shared` |
| `run build` | Kiểm kiểu rồi build `admin` và `pos` |

Địa chỉ gateway lấy từ biến `VITE_API_URL`, mặc định `http://localhost:8080`.

## Phiên đăng nhập

- Access token chỉ nằm trong bộ nhớ. Refresh token nằm ở `localStorage` để mở lại phiên sau khi tải lại trang; `AuthProvider` đổi nó lấy cặp token mới lúc ứng dụng khởi động.
- Các request gặp 401 cùng lúc dùng chung một lần làm mới, vì refresh token xoay vòng: dùng lại token cũ thì cả chuỗi bị thu hồi. Giữa các tab, việc làm mới xếp hàng bằng Web Locks và mỗi tab đọc lại token trong `localStorage` sau khi tới lượt.
- Làm mới bị từ chối thì phiên kết thúc và `RequireRole` đưa người dùng về `/login`.
- Vai trò lấy từ `user` trong phản hồi đăng nhập, cùng giá trị với claim `role` của token.

## Cấu trúc thư mục

```text
frontend/<app>/src/
├─ app/         router, layout, menu theo vai trò
├─ pages/       mỗi route một thư mục; chỉ ghép các phần của features
└─ features/    mỗi nghiệp vụ một thư mục: component, hook, kiểu riêng
```

- `pages` gọi `features`; `features` gọi `frontend/shared`. Không gọi ngược.
- `admin` và `pos` không import của nhau. Thứ dùng chung chuyển sang `shared`.
- Một component một file, tên PascalCase. Hook bắt đầu bằng `use`.

## Gọi API

- Mọi lời gọi đi qua `frontend/shared/src/api`. Component không tự gọi `fetch`.
- Client tự gắn access token, tự làm mới token một lần khi gặp 401, và đọc lỗi theo ProblemDetails.
- Kiểu dữ liệu vào ra của API khai báo ở `frontend/shared/src/types`, khớp [design/api/](../design/api/README.md).
- Xử lý lỗi theo `code`, không theo chuỗi thông báo:

| `code` | Giao diện làm gì |
| --- | --- |
| `validation_failed` | Hiện lỗi cạnh từng trường |
| `insufficient_stock` | Hiện tên SKU và số còn lại từ `details` |
| `invalid_state_transition` | Báo trạng thái đã đổi và tải lại bản ghi |
| `reference_not_ready` | Tự thử lại tới 3 lần, cách nhau 1 giây |
| `forbidden` | Báo không có quyền; không ẩn lỗi |
| `not_found` | Đưa về danh sách |

## Quyền

- Route và menu lọc theo vai trò lấy từ token, qua `frontend/shared/src/auth`.
- Ẩn nút chỉ để giao diện gọn. Quyền thật do backend kiểm; đừng dựa vào việc ẩn nút.
- Trường mà vai trò không được xem (giá vốn, lợi nhuận với Staff) không có trong phản hồi API; giao diện không tự tính ra chúng.

## Tiền, số và thời gian

- Tiền hiển thị theo định dạng Việt Nam, không có phần thập phân, ví dụ `150.000 ₫`.
- Thời gian nhận từ API là UTC; hiển thị theo giờ Việt Nam.
- Không làm phép tính tiền bằng số thực ở giao diện ngoài việc cộng tổng giỏ để hiển thị; số chính thức là số backend trả về.

## Riêng cho POS

- Ô tìm kiếm luôn giữ focus; mọi popup đóng xong phải trả focus về ô tìm.
- Vùng chạm tối thiểu 44 px.
- Mỗi lần bấm thanh toán sinh một `Idempotency-Key`; khóa nút trong lúc chờ; thử lại thì dùng lại khóa cũ.
- Chỉ in sau khi backend trả đơn đã lưu.
- Service worker chỉ cache app shell. Không lưu đơn để gửi sau.

## Test

- Logic thuần (tính tổng giỏ, định dạng tiền, lọc theo vai trò) có test đơn vị bằng Vitest.
- Luồng màn hình kiểm tay theo tiêu chí chấp nhận của use case ở cuối mỗi phase.

## Điều không làm

- Không gọi thẳng cổng của service; chỉ gọi gateway.
- Không lưu access token ở nơi script bên thứ ba đọc được ngoài bộ nhớ của ứng dụng và cơ chế lưu do `shared/auth` quản lý.
- Không sao chép quy tắc nghiệp vụ của backend sang giao diện để "kiểm trước cho chắc"; kiểm sớm chỉ để báo sớm.
