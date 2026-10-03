# Giao diện: admin

Trang quản trị cho Owner và Staff, nằm ở `frontend/admin`. Mỗi dòng dưới đây là một màn hình với route, vai trò được vào, API nó gọi và task dựng nó. Cashier không vào được ứng dụng này.

## Màn hình

| Route | Màn hình | Vai trò | API chính | Use case | Task |
| --- | --- | --- | --- | --- | --- |
| `/login` | Đăng nhập | Công khai | `POST /api/identity/auth/login` | UC-AUTH-02 | W1-09 |
| `/register` | Đăng ký tenant | Công khai | `POST /api/identity/tenants` | UC-AUTH-01 | W1-09 |
| `/` | Tổng quan: cảnh báo tồn, đơn chờ duyệt | Owner, Staff | `GET /api/insights/alerts/low-stock`, `GET /api/core/orders?status=Reserved` | UC-REP-03 | W4-07 |
| `/branches` | Chi nhánh | Owner | `/api/identity/branches` | UC-AUTH-04 | W1-10 |
| `/users` | Người dùng | Owner | `/api/identity/users` | UC-AUTH-03 | W1-10 |
| `/catalog/categories` | Danh mục | Owner, Staff | `/api/catalog/categories` | UC-PROD-01 | W1-10 |
| `/catalog/brands` | Thương hiệu | Owner, Staff | `/api/catalog/brands` | UC-PROD-01 | W1-10 |
| `/catalog/products` | Danh sách sản phẩm | Owner, Staff | `GET /api/catalog/products` | UC-PROD-02 | W2-07 |
| `/catalog/products/:id` | Sản phẩm, SKU, mã vạch, giá | Owner, Staff; ô giá chỉ Owner sửa | `/api/catalog/products/{id}`, `/skus/{id}/barcodes`, `/skus/{id}/prices` | UC-PROD-02, 03, 04 | W2-07 |
| `/inventory/stock` | Tồn theo chi nhánh, đặt ngưỡng | Owner, Staff | `GET /api/core/stock`, `PUT /api/core/stock/threshold` | UC-INV-01, UC-INV-05 | W2-08 |
| `/inventory/ledger` | Sổ giao dịch, chỉ xem | Owner, Staff | `GET /api/core/ledger` | UC-INV-01 | W2-08 |
| `/inventory/receipts` | Phiếu nhập: danh sách, tạo, xác nhận | Owner, Staff | `/api/core/purchase-receipts` | UC-INV-02 | W2-08 |
| `/inventory/transfers` | Chuyển kho: tạo, xuất, nhận | Owner, Staff | `/api/core/transfers` | UC-INV-03 | W4-06 |
| `/inventory/stocktakes` | Kiểm kê: mở phiên, nhập số đếm, chốt | Owner, Staff | `/api/core/stocktakes` | UC-INV-04 | W4-06 |
| `/orders` | Đơn đa kênh, lọc theo trạng thái, kênh, chi nhánh | Owner, Staff | `GET /api/core/orders` | UC-ORD-06 | W3-08 |
| `/orders/new` | Tạo đơn thủ công | Owner, Staff | `POST /api/core/orders`, `GET /api/core/pos/skus` | UC-ORD-02 | W3-08 |
| `/orders/:id` | Chi tiết đơn: duyệt, hoàn tất, hủy, in phiếu giao | Owner, Staff | `/api/core/orders/{id}` và ba hành động | UC-ORD-03, 04, 06 | W3-08 |
| `/reports/gross-profit` | Doanh thu, giá vốn, lợi nhuận gộp | Owner | `GET /api/insights/reports/gross-profit` | UC-REP-01 | W4-07 |
| `/reports/inventory` | Giá trị tồn, bán chạy, bán chậm | Owner, Staff | Ba API báo cáo tồn | UC-REP-02 | W4-07 |
| `/forecast` | Đề xuất nhập hàng, chạy dự báo | Owner | `/api/insights/forecast/*` | UC-AI-01, UC-AI-02 | W4-07 |
| `/channel` | Shop của sàn, webhook đã nhận, bắn tải | Owner | `/api/channel/*` | UC-SIM-01 | Mức P2, làm sau W3-08 nếu kịp; không kịp thì dùng Swagger của `channel` |

## Hành vi chung

- Menu chỉ hiện mục mà vai trò được vào; route bị chặn theo vai trò ở `frontend/shared/auth`. Backend vẫn là nơi quyết định quyền.
- Mọi bảng danh sách có phân trang phía server.
- Lỗi 409 `insufficient_stock` hiện tên SKU và số còn lại, lấy từ `details` của phản hồi.
- Lỗi 409 `reference_not_ready` được `frontend/shared/api` tự thử lại tới 3 lần, cách nhau 1 giây, rồi mới báo lỗi.
- Giá vốn và lợi nhuận không hiện với Staff: các trường đó không có trong phản hồi của backend.
- Màn hình sổ giao dịch không có nút sửa hay xóa.
- Thông báo `OrderCreated` và `LowStock` hiện popup kèm âm thanh ở mọi màn hình; bấm vào mở chi tiết đơn hoặc màn hình tồn.

## In phiếu giao

Trang `/orders/:id` có nút in phiếu giao. Nút mở một vùng nội dung chỉ dành cho in (mã đơn, kênh, chi nhánh, danh sách hàng, số lượng) và gọi hộp thoại in của trình duyệt.
