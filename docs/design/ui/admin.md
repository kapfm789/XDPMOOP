# Giao diện: admin

Trang quản trị cho Owner và Staff, nằm ở `frontend/admin`. Mỗi dòng dưới đây là một màn hình với route, vai trò được vào, API nó gọi và task dựng nó. Cashier không vào được ứng dụng này.

## Màn hình

| Route | Màn hình | Vai trò | API chính | Use case | Task |
| --- | --- | --- | --- | --- | --- |
| `/login` | Đăng nhập | Công khai | `POST /api/identity/auth/login` | UC-AUTH-02 | W1-09 |
| `/register` | Đăng ký tenant | Công khai | `POST /api/identity/tenants` | UC-AUTH-01 | W1-09 |
| `/` | Tổng quan: thẻ số liệu (doanh thu hôm nay chỉ Owner, số đơn chờ duyệt, số SKU tồn thấp), bảng cảnh báo tồn, danh sách đơn chờ duyệt, biểu đồ doanh thu 7 ngày gần nhất (chỉ Owner) | Owner, Staff | `GET /api/insights/alerts/low-stock`, `GET /api/core/orders?status=Reserved`; Owner thêm `GET /api/insights/reports/gross-profit` (`groupBy=day`) | UC-REP-01, UC-REP-03 | W4-07, W5-10 |
| `/branches` | Chi nhánh | Owner | `/api/identity/branches` | UC-AUTH-04 | W1-10 |
| `/users` | Người dùng | Owner | `/api/identity/users` | UC-AUTH-03 | W1-10 |
| `/catalog/categories` | Danh mục | Owner, Staff | `/api/catalog/categories` | UC-PROD-01 | W1-10 |
| `/catalog/brands` | Thương hiệu | Owner, Staff | `/api/catalog/brands` | UC-PROD-01 | W1-10 |
| `/catalog/products` | Danh sách sản phẩm | Owner, Staff | `GET /api/catalog/products` | UC-PROD-02 | W2-07 |
| `/catalog/products/:id` | Sản phẩm, SKU, mã vạch, giá | Owner, Staff; ô giá chỉ Owner sửa | `/api/catalog/products/{id}`, `/skus/{id}/barcodes`, `/skus/{id}/prices` | UC-PROD-02, 03, 04 | W2-07 |
| `/inventory/stock` | Tồn theo chi nhánh, đặt ngưỡng; dải tổng hợp trên bảng: tổng giá trị tồn (chỉ Owner), số dòng tồn đang theo dõi, số SKU tồn thấp | Owner, Staff | `GET /api/core/stock`, `PUT /api/core/stock/threshold`; dải tổng hợp đọc `GET /api/insights/reports/inventory-value` (`totalValue`) và `GET /api/insights/alerts/low-stock` | UC-INV-01, UC-INV-05, UC-REP-02 | W2-08, W5-10 |
| `/inventory/ledger` | Sổ giao dịch, chỉ xem | Owner, Staff | `GET /api/core/ledger` | UC-INV-01 | W2-08 |
| `/inventory/receipts` | Phiếu nhập: danh sách, tạo, sửa, xác nhận; thêm nhà cung cấp | Owner, Staff | `/api/core/purchase-receipts`, `/api/core/suppliers`; tìm SKU qua `/api/catalog/products` | UC-INV-02 | W2-08 |
| `/inventory/transfers` | Chuyển kho: tạo, xuất, nhận, xóa phiếu nháp | Owner, Staff | `/api/core/transfers`; tìm SKU qua `/api/catalog/products` | UC-INV-03 | W4-06 |
| `/inventory/stocktakes` | Kiểm kê: mở phiên, nhập số đếm, chốt | Owner, Staff | `/api/core/stocktakes`; nạp SKU của chi nhánh qua `GET /api/core/stock`; tìm SKU qua `/api/catalog/products` | UC-INV-04 | W4-06 |
| `/orders` | Đơn đa kênh, lọc theo trạng thái (tab kèm số đơn mỗi trạng thái), kênh, chi nhánh, khoảng thời gian | Owner, Staff | `GET /api/core/orders`; số trên tab là `total` của lời gọi lọc theo trạng thái đó | UC-ORD-06 | W3-08, W5-10 |
| `/orders/new` | Tạo đơn thủ công | Owner, Staff | `POST /api/core/orders`, `GET /api/core/pos/skus` | UC-ORD-02 | W3-08 |
| `/orders/:id` | Chi tiết đơn: duyệt, hoàn tất, hủy, in phiếu giao | Owner, Staff | `/api/core/orders/{id}` và ba hành động | UC-ORD-03, 04, 06 | W3-08 |
| `/reports/gross-profit` | Doanh thu, giá vốn, lợi nhuận gộp | Owner | `GET /api/insights/reports/gross-profit` | UC-REP-01 | W4-07 |
| `/reports/inventory` | Giá trị tồn, bán chạy, bán chậm | Owner, Staff | Ba API báo cáo tồn | UC-REP-02 | W4-07 |
| `/forecast` | Đề xuất nhập hàng, chạy dự báo | Owner | `/api/insights/forecast/*` | UC-AI-01, UC-AI-02 | W4-07 |
| `/channel` | Shop của sàn, webhook đã nhận, bắn tải | Owner | `/api/channel/channel-shops`, `/api/channel/simulator/burst`, `/api/channel/webhook-events` | UC-SIM-01 | W3-08 |

## Hành vi chung

- Menu chỉ hiện mục mà vai trò được vào; route bị chặn theo vai trò ở `frontend/shared/auth`. Backend vẫn là nơi quyết định quyền.
- Mọi bảng danh sách có phân trang phía server.
- Lỗi 409 `insufficient_stock` hiện tên SKU và số còn lại, lấy từ `details` của phản hồi.
- Lỗi 409 `reference_not_ready` được `frontend/shared/api` tự thử lại tới 3 lần, cách nhau 1 giây, rồi mới báo lỗi.
- Giá vốn và lợi nhuận không hiện với Staff: các trường đó không có trong phản hồi của backend.
- Màn hình sổ giao dịch không có nút sửa hay xóa.
- Thông báo `OrderCreated` và `LowStock` hiện popup kèm âm thanh ở mọi màn hình, góc dưới bên phải, giữ 10 giây; bấm vào mở chi tiết đơn hoặc màn hình tồn. Danh sách đơn, danh sách tồn và cảnh báo tồn ở trang tổng quan đang mở tự tải lại khi có thông báo tương ứng; cảnh báo tồn còn tự tải lại mỗi phút vì SKU lên lại trên ngưỡng không sinh thông báo.

## Bố cục và phong cách

Áp từ bản thiết kế Figma của nhóm (task W5-10). Chỉ dùng Ant Design: màu, bo góc và font đặt bằng theme token của `ConfigProvider`; icon menu là SVG viết trong `frontend/admin`, không thêm package icon.

| Hạng mục | Thiết kế |
| --- | --- |
| Màu và chữ | Màu chính `#1677ff`, nền trang `#f5f7fa`, nền thẻ trắng, viền `#e4e9f0`, chữ `#172033`, chữ phụ `#697386`; bo góc 6 tới 7 px; font Inter. Xanh lá là đủ hàng hoặc thành công, cam là sắp hết hoặc đang chờ, đỏ là hết hàng hoặc lỗi |
| Thanh bên | Nền trắng, logo OISM; menu chia nhóm có icon: Tổng quan; Sản phẩm (Danh mục, Thương hiệu, Sản phẩm); Kho (Tồn kho, Sổ giao dịch, Phiếu nhập, Chuyển kho, Kiểm kê); Đơn hàng (Danh sách đơn, Tạo đơn); Báo cáo (Lợi nhuận gộp, Tồn kho và tốc độ bán, Dự báo nhập hàng); Kênh sàn; Cài đặt (Chi nhánh, Người dùng). Thu gọn được còn icon. Mục và nhóm vẫn lọc theo vai trò như bảng trên |
| Thanh trên | Tên cửa hàng lấy từ `tenantName` của `GET /api/identity/me`; chuông thông báo; avatar chữ cái đầu, họ tên và tag vai trò (Chủ cửa hàng, Nhân viên); nút đăng xuất |
| Đầu trang | Breadcrumb theo nhóm menu, tiêu đề trang; dưới đó là hàng bộ lọc rồi tới các thẻ nội dung có tiêu đề |
| Bảng | Đầu bảng nền xám nhạt, dòng cao, mã chứng từ in đậm màu chính, trạng thái là tag màu, phân trang phía server ở chân thẻ |
| Kênh | Tag màu theo kênh: Shopee cam, TikTok đen, Lazada xanh đậm, POS và Admin xanh |
| Chi tiết đơn | Đầu trang có mã đơn, tag trạng thái và các nút hành động; thanh bước Tạo, Giữ hàng, Đã duyệt, Hoàn tất; bảng hàng kèm tổng tiền; khung phần giữ hàng; khung thanh toán chỉ với đơn POS |
| Đăng nhập, đăng ký | Thẻ ở giữa trang trên nền gradient nhạt, logo OISM ở trên |
| Kích thước | Thiết kế cho màn hình từ 1180 px; hẹp hơn thì thanh bên tự thu gọn |

### Không lấy từ bản Figma

Những mục dưới có trong bản Figma nhưng không có trong use case hay API, nên không làm. Muốn làm thì thêm yêu cầu và thiết kế trước.

| Mục trong Figma | Lý do không làm |
| --- | --- |
| Nút đổi giao diện tối | Không có yêu cầu |
| Nút "Xuất báo cáo", "Xuất dữ liệu", "Xuất sổ" | Không có API xuất file |
| Chọn tenant ở thanh trên | Mỗi người dùng chỉ thuộc một tenant ([ADR-0011](../../decisions/0011-auth-and-roles.md)) |
| Khung hỗ trợ "help@oism.vn" ở thanh bên, dòng "Dữ liệu cập nhật lúc ..." | Không có trong yêu cầu |
| Cột tồn khả dụng ở danh sách sản phẩm | `catalog` không giữ tồn; tồn xem ở `/inventory/stock` |
| Dự báo: cột "Độ tin cậy", thẻ "Độ chính xác dự báo", nút "Tạo phiếu nhập" trên từng dòng | API không trả độ tin cậy; sai số là hai chỉ số WAPE. Đề xuất chỉ là khuyến nghị và không dẫn tới phiếu nhập ([ADR-0010](../../decisions/0010-ai-forecast.md)) |
| Thanh toán "qua sàn, chờ đối soát" ở đơn sàn | Chỉ đơn POS có thanh toán ([ADR-0007](../../decisions/0007-pos-payment-no-offline.md)) |
| Cột "Lần hoạt động cuối" của người dùng, "SKU đang có" của chi nhánh, "Tiến độ" và tổng chênh lệch ở danh sách kiểm kê | API không trả các trường này |
| Mã chứng từ dạng số thứ tự (`#GD1048`, `PN00241`, `CK00128`) | Mã thật do `core` đặt: `DH`, `PN`, `CK` kèm 12 ký tự hex; dòng sổ dùng `seq` |

## In phiếu giao

Trang `/orders/:id` có nút in phiếu giao. Nút mở một vùng nội dung chỉ dành cho in (mã đơn, kênh, chi nhánh, danh sách hàng, số lượng) và gọi hộp thoại in của trình duyệt.
