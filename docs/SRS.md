# Software Requirements Specification (SRS)
## Hệ thống quản lý bán hàng và tồn kho đa kênh OISM

---

## Phiên bản tài liệu

| Phiên bản | Ngày | Người sửa | Mô tả thay đổi |
| --- | --- | --- | --- |
| 1.0 | 15/10/2026 | Frontend Team | Tạo tài liệu SRS dựa trên requirements hiện có |

---

## 1. Giới thiệu

### 1.1 Mục đích tài liệu

Tài liệu này mô tả các yêu cầu chức năng và phi chức năng của hệ thống OISM (Omnichannel Inventory and Sales Management System). Tài liệu phục vụ cho:
- Đội ngũ phát triển hiểu rõ yêu cầu
- Đội ngũ kiểm thử xác định tiêu chí chấp nhận
- Quản lý dự án theo dõi tiến độ

### 1.2 Phạm vi dự án

OISM là hệ thống SaaS multi-tenant giúp cửa hàng bán lẻ quản lý đơn hàng và tồn kho trên nhiều kênh cùng lúc:
- Quầy POS tại cửa hàng
- Các sàn thương mại điện tử: Shopee, TikTok Shop, Lazada

Hệ thống giải quyết 3 vấn đề chính:
1. Bán vượt tồn do đồng bộ chậm giữa các kênh
2. Giá vốn hàng bán (COGS) sai do tính thủ công
3. Số liệu lệch do sửa tồn bằng tay

### 1.3 Định nghĩa

| Thuật ngữ | Định nghĩa |
| --- | --- |
| Tenant | Khách hàng sử dụng hệ thống (mỗi cửa hàng là một tenant) |
| SKU | Stock Keeping Unit - Mã định danh duy nhất cho mỗi biến thể sản phẩm |
| POS | Point of Sale - Quầy bán hàng tại cửa hàng |
| Channel | Kênh bán hàng (POS, Shopee, TikTok, Lazada) |
| On-hand | Tồn thực tế trong kho |
| Reserved | Số lượng đang giữ cho đơn hàng chưa xác nhận |
| Available | Tồn khả dụng = On-hand - Reserved |
| Threshold | Ngưỡng tồn tối thiểu để cảnh báo nhập hàng |
| COGS | Cost of Goods Sold - Giá vốn hàng bán |

### 1.4 Tài liệu tham khảo

- Đề bài gốc: `OISM-ban-dich-va-phan-tich-tieng-Viet.md`
- Yêu cầu chức năng: `docs/requirements/functional.md`
- Yêu cầu phi chức năng: `docs/requirements/non-functional.md`
- Use case: `docs/usecase-userstory/`
- Kiến trúc: `docs/architecture/`
- Quyết định thiết kế: `docs/decisions/`

---

## 2. Mô tả tổng quan

### 2.1 Triển vọng sản phẩm

OISM là hệ thống quản lý bán hàng và tồn kho đa kênh với các đặc điểm:
- Multi-tenant: Mỗi tenant có không gian dữ liệu riêng
- Real-time: Đồng bộ tồn kho ngay lập tức giữa các kênh
- Omni-channel: Đơn từ mọi kênh được chuẩn hóa về một mô hình chung
- Cloud-native: Microservices architecture với .NET 8
- Modern UI: React với Ant Design cho Admin và POS

### 2.2 Chức năng sản phẩm

Hệ thống bao gồm 9 nhóm chức năng chính:

1. **FR-AUTH: Tenant và xác thực**
   - Đăng ký tenant mới
   - Đăng nhập/đăng xuất với JWT
   - Phân quyền theo vai trò (Owner, Staff, Cashier)
   - Quản lý chi nhánh

2. **FR-PROD: Sản phẩm và biến thể**
   - Quản lý danh mục phân cấp
   - Quản lý thương hiệu
   - Sản phẩm đơn và sản phẩm có biến thể
   - Quản lý mã vạch
   - Quản lý giá lẻ và giá sỉ

3. **FR-INV: Sổ giao dịch tồn kho**
   - Sổ kho chỉ thêm mới (không sửa trực tiếp tồn)
   - Phiếu nhập từ nhà cung cấp
   - Chuyển kho hai bước
   - Kiểm kê và điều chỉnh

4. **FR-COST: Giá vốn bình quân gia quyền**
   - Tính giá vốn khi xác nhận phiếu nhập
   - Chốt giá vốn vào dòng đơn hàng

5. **FR-ORD: Trung tâm đơn hàng**
   - Chuẩn hóa đơn từ mọi kênh
   - Máy trạng thái đơn hàng
   - Duyệt, hoàn tất, hủy đơn

6. **FR-RSE: Chống bán vượt tồn**
   - Duy trì available = on-hand - reserved
   - Chặn đơn nếu không đủ hàng
   - Giữ hàng cho đơn online

7. **FR-POS: Bán tại quầy**
   - Giao diện bán nhanh
   - Quét mã vạch
   - Thanh toán tiền mặt hoặc QR
   - In hóa đơn

8. **FR-REP: Báo cáo và cảnh báo**
   - Báo cáo doanh thu và lợi nhuận gộp
   - Báo cáo giá trị tồn
   - Cảnh báo tồn thấp

9. **FR-AI: Dự báo nhập hàng**
   - Dự báo nhu cầu bán
   - Đề xuất số lượng nhập
   - Backtest độ chính xác

### 2.3 Đặc điểm người dùng

| Vai trò | Mô tả | Quyền hạn |
| --- | --- | --- |
| Owner | Chủ cửa hàng | Toàn quyền: quản lý tenant, người dùng, chi nhánh, xem báo cáo lợi nhuận, đề xuất nhập hàng |
| Staff | Nhân viên quản lý | Quản lý kho, đơn hàng, sản phẩm, xem báo cáo tồn |
| Cashier | Thu ngân | Chỉ dùng POS để bán hàng |

### 2.4 Ràng buộc

#### Ràng buộc kỹ thuật
- Backend: .NET 8, PostgreSQL, RabbitMQ
- Frontend: React, Vite, TypeScript, Ant Design
- Architecture: Microservices với API Gateway
- Multi-tenant: Mọi bảng nghiệp vụ có TenantId

#### Ràng buộc nghiệp vụ
- Sổ kho chỉ thêm mới, không sửa trực tiếp
- Đơn hàng và tồn kho phải trong cùng transaction
- Không bán vượt tồn khả dụng
- Giá vốn chốt một lần khi xác nhận đơn

#### Ràng buộc bảo mật
- Mật khẩu băm BCrypt
- HTTPS TLS 1.3
- Access token 60 phút
- Phân quyền theo vai trò

### 2.5 Giả định và phụ thuộc

- Giả định: Người dùng có kết nối internet khi dùng POS (ADR-0007: không bán offline)
- Phụ thuộc: Backend services phải chạy trước khi frontend hoạt động
- Phụ thuộc: PostgreSQL và RabbitMQ phải được cấu hình đúng

---

## 3. Yêu cầu chức năng

Yêu cầu chức năng được chia thành 9 nhóm, tổng cộng 30 yêu cầu.

### 3.1 FR-AUTH: Tenant và xác thực

| Mã | Yêu cầu | Mô tả chi tiết | Use case | Service |
| --- | --- | --- | --- | --- |
| FR-AUTH-01 | Tạo tenant mới | Mỗi tenant có không gian dữ liệu riêng và TenantId duy nhất | UC-AUTH-01 | identity |
| FR-AUTH-02 | Đăng nhập/đăng xuất | Đăng nhập bằng email hoặc số điện thoại + mật khẩu; trả JWT chứa TenantId, UserId, Role | UC-AUTH-02 | identity |
| FR-AUTH-03 | Phân quyền theo vai trò | Kiểm soát truy cập API và màn hình: Owner toàn quyền, Staff quản lý kho/đơn, Cashier chỉ dùng POS | UC-AUTH-03 | identity, gateway, mọi service |
| FR-AUTH-04 | Quản lý chi nhánh | Owner tạo, sửa, bật/tắt chi nhánh và kho | UC-AUTH-04 | identity |

### 3.2 FR-PROD: Sản phẩm và biến thể

| Mã | Yêu cầu | Mô tả chi tiết | Use case | Service |
| --- | --- | --- | --- | --- |
| FR-PROD-01 | Danh mục và thương hiệu | Quản lý danh mục phân cấp và danh sách thương hiệu | UC-PROD-01 | catalog |
| FR-PROD-02 | Sản phẩm và biến thể | Sản phẩm đơn và sản phẩm có biến thể (màu, cỡ); mỗi biến thể có SKU duy nhất trong tenant | UC-PROD-02 | catalog |
| FR-PROD-03 | Mã vạch | Tự sinh hoặc nhập tay mã vạch EAN-13 hoặc Code128 cho từng SKU | UC-PROD-03 | catalog |
| FR-PROD-04 | Giá lẻ và giá sỉ | Giá lẻ và giá sỉ cho từng SKU, tách khỏi giá vốn | UC-PROD-04 | catalog |

### 3.3 FR-INV: Sổ giao dịch tồn kho

| Mã | Yêu cầu | Mô tả chi tiết | Use case | Service |
| --- | --- | --- | --- | --- |
| FR-INV-01 | Sổ kho chỉ thêm mới | Mọi biến động tồn (nhập, xuất, bán, trả, kiểm kê, chuyển kho) ghi vào InventoryTransaction chỉ thêm mới; không sửa trực tiếp cột tồn | UC-INV-01 | core |
| FR-INV-02 | Phiếu nhập | Phiếu nhập từ nhà cung cấp làm tăng on_hand và cập nhật giá vốn bình quân | UC-INV-02 | core |
| FR-INV-03 | Chuyển kho hai bước | Xuất vào trạng thái đang vận chuyển, rồi xác nhận nhập tại nơi nhận | UC-INV-03 | core |
| FR-INV-04 | Kiểm kê | Phiên kiểm kê: ghi số đếm, so với số trên sổ, tạo bút toán điều chỉnh | UC-INV-04 | core |

### 3.4 FR-COST: Giá vốn bình quân gia quyền

| Mã | Yêu cầu | Mô tả chi tiết | Use case | Service |
| --- | --- | --- | --- | --- |
| FR-COST-01 | Tính giá vốn | Khi xác nhận phiếu nhập, tính lại: (tồn cũ × giá vốn cũ + SL nhập × đơn giá nhập) / (tồn cũ + SL nhập) | UC-INV-02 | core |
| FR-COST-02 | Chốt giá vốn | Lưu cố định giá vốn vào OrderItem.CostPrice khi đơn sang Confirmed hoặc khi POS thanh toán xong | UC-ORD-03, UC-POS-02 | core |

### 3.5 FR-ORD: Trung tâm đơn hàng

| Mã | Yêu cầu | Mô tả chi tiết | Use case | Service |
| --- | --- | --- | --- | --- |
| FR-ORD-01 | Chuẩn hóa đơn | Chuẩn hóa đơn từ POS, đơn thủ công trên admin và webhook giả lập về Canonical Order Model | UC-ORD-01, UC-ORD-02, UC-POS-02 | core, channel |
| FR-ORD-02 | Máy trạng thái đơn | Draft → Reserved → Confirmed → Completed, hoặc Cancelled; chặn bước chuyển sai | UC-ORD-03 đến UC-ORD-06 | core |
| FR-ORD-03 | Quản lý đơn | Nhân viên xem đơn chờ xử lý, duyệt, in phiếu giao hàng hoặc hủy | UC-ORD-03, UC-ORD-04, UC-ORD-06 | core, admin |

### 3.6 FR-RSE: Chống bán vượt tồn

| Mã | Yêu cầu | Mô tả chi tiết | Use case | Service |
| --- | --- | --- | --- | --- |
| FR-RSE-01 | Duy trì available | Duy trì available = on_hand - reserved cho từng SKU tại từng chi nhánh | UC-INV-01 | core |
| FR-RSE-02 | Chặn đơn vượt | Đơn online vào Reserved thì tăng reserved; từ chối đơn nếu available nhỏ hơn số lượng đặt | UC-ORD-01 | core |
| FR-RSE-03 | Giải phóng reserved | Sang Confirmed: giảm on_hand, giảm reserved, ghi ledger. Sang Cancelled: giảm reserved | UC-ORD-03, UC-ORD-04, UC-ORD-05 | core |

### 3.7 FR-POS: Bán tại quầy

| Mã | Yêu cầu | Mô tả chi tiết | Use case | Service |
| --- | --- | --- | --- | --- |
| FR-POS-01 | Giao diện bán nhanh | Giao diện bán nhanh cho cảm ứng và phím tắt; tìm theo tên, SKU, mã vạch | UC-POS-01 | pos |
| FR-POS-02 | Kiểm tồn khi bán | Kiểm available ngay khi chọn sản phẩm; chặn bán vượt để bảo vệ hàng đã giữ cho đơn online | UC-POS-01, UC-POS-02 | core, pos |
| FR-POS-03 | Thanh toán | Thanh toán tiền mặt hoặc chuyển khoản QR; mở hộp thoại in của trình duyệt | UC-POS-02, UC-POS-03 | pos |
| FR-POS-04 | Checkout nguyên tử | Checkout POS chạy giữ hàng → xác nhận → trừ hàng trong một transaction nguyên tử | UC-POS-02 | core |

### 3.8 FR-REP: Báo cáo và cảnh báo

| Mã | Yêu cầu | Mô tả chi tiết | Use case | Service |
| --- | --- | --- | --- | --- |
| FR-REP-01 | Báo cáo lợi nhuận | Doanh thu thuần, giá vốn hàng bán, lợi nhuận gộp; lọc theo thời gian, chi nhánh, kênh, SKU | UC-REP-01 | insights |
| FR-REP-02 | Báo cáo tồn | Giá trị tồn theo giá vốn; hàng bán chạy và hàng bán chậm | UC-REP-02 | insights |
| FR-REP-03 | Cảnh báo tồn thấp | Cảnh báo trên dashboard khi available ≤ Threshold của SKU | UC-REP-03, UC-INV-05 | insights, core |

### 3.9 FR-SIM: Giả lập và thời gian thực

| Mã | Yêu cầu | Mô tả chi tiết | Use case | Service |
| --- | --- | --- | --- | --- |
| FR-SIM-01 | Webhook giả lập | Công cụ gửi webhook giả lập Shopee, TikTok, Lazada để thử tải và cơ chế khóa | UC-SIM-01 | channel |
| FR-SIM-02 | Thông báo realtime | SignalR đẩy thông báo đơn mới tới admin và POS mà không tải lại trang | UC-SIM-02 | insights, frontend |
| FR-SIM-03 | Job nền | Hangfire tự hủy đơn giữ hàng đã hết hạn và tổng hợp báo cáo hằng ngày | UC-ORD-05, UC-REP-01 | core, insights |

### 3.10 FR-AI: Dự báo nhập hàng

| Mã | Yêu cầu | Mô tả chi tiết | Use case | Service |
| --- | --- | --- | --- | --- |
| FR-AI-01 | Dự báo nhu cầu | Dự báo nhu cầu bán theo từng SKU tại từng chi nhánh từ lịch sử bán | UC-AI-02 | insights |
| FR-AI-02 | Đề xuất nhập | Hiển thị đề xuất số lượng nên nhập trên admin; chỉ là khuyến nghị, không tự tạo phiếu | UC-AI-01 | insights, admin |
| FR-AI-03 | Backtest | Backtest dự báo trên lịch sử và báo cáo sai số so với một phương án cơ sở | UC-AI-02 | insights |

---

## 4. Yêu cầu phi chức năng

Yêu cầu phi chức năng được chia thành 5 nhóm, tổng cộng 11 yêu cầu.

### 4.1 NFR-PERF: Hiệu năng

| Mã | Yêu cầu | Chiến thuật kiến trúc | Cách kiểm chứng |
| --- | --- | --- | --- |
| NFR-PERF-01 | p95 tìm SKU hoặc quét mã dưới 200 ms; tạo đơn và giao dịch ghi dưới 500 ms; báo cáo doanh thu dưới 2 giây với dưới 100.000 bản ghi | core giữ bản sao SkuRef để trả SKU kèm tồn trong một lần gọi; chỉ mục theo tenant; báo cáo đọc bảng tổng hợp ngày | Đo bằng k6, ghi cấu hình máy và lượng dữ liệu (T17) |
| NFR-PERF-02 | 50 đơn đồng thời mua đơn vị cuối cùng: tuyệt đối không âm tồn khả dụng | Khóa bi quan SELECT ... FOR UPDATE trên dòng số dư; CHECK reserved ≤ on_hand ở database | Test tích hợp trên PostgreSQL thật, chạy lặp (T01, T02) |

### 4.2 NFR-TENANT: Multi-tenant

| Mã | Yêu cầu | Chiến thuật kiến trúc | Cách kiểm chứng |
| --- | --- | --- | --- |
| NFR-TENANT-01 | Mọi bảng nghiệp vụ có TenantId; bắt buộc EF Core Global Query Filters | Xem multi-tenancy.md | Dùng ID của tenant khác trên API, event, SignalR, job (T14, T15) |
| NFR-TENANT-02 | Chỉ mục (TenantId, CreatedAt) và (TenantId, SKUId) cho ledger và đơn | Chỉ mục khai báo trong migration; thêm BranchId vì tồn quản lý theo chi nhánh | EXPLAIN cho truy vấn ledger và đơn dùng index |

### 4.3 NFR-SEC: Bảo mật

| Mã | Yêu cầu | Chiến thuật kiến trúc | Cách kiểm chứng |
| --- | --- | --- | --- |
| NFR-SEC-01 | Mật khẩu băm BCrypt hoặc Argon2; HTTPS TLS 1.3; access token 60 phút; refresh token an toàn | Xem security.md | Test đăng nhập và phân quyền; kiểm cấu hình TLS ở gateway |
| NFR-SEC-02 | Tạo đơn → giữ hàng → trừ hàng trong một transaction nguyên tử, rollback toàn bộ khi lỗi | Đơn và kho chung service core; mỗi use case một transaction; event qua outbox | Gây lỗi giữa chừng và kiểm không có gì thay đổi (T05, T23) |
| NFR-SEC-03 | Ledger chỉ thêm mới; cấm UPDATE, DELETE; sửa sai bằng giao dịch bù trừ | Trigger database từ chối sửa và xóa; không có API sửa ledger | Thử UPDATE và DELETE trực tiếp (T16) |

### 4.4 NFR-USA: Usability

| Mã | Yêu cầu | Chiến thuật kiến trúc | Cách kiểm chứng |
| --- | --- | --- | --- |
| NFR-USA-01 | 99,5% thời gian hoạt động trong khung 07:00 đến 22:00 | Health check và restart policy cho mọi container | Không đo được trong 5 tuần; chỉ kiểm cấu hình và kịch bản khởi động lại (T20) |
| NFR-USA-02 | POS hoàn tất thanh toán dưới 3 lần nhấp, hoặc quét mã rồi Enter; chạy trên desktop, tablet, thiết bị cầm tay | Một màn hình bán hàng, phím tắt, ô tìm kiếm luôn giữ focus | Đếm thao tác với một sản phẩm, tiền mặt, không giảm giá; thử ba kích thước màn hình |

### 4.5 NFR-MAINT: Maintainability

| Mã | Yêu cầu | Chiến thuật kiến trúc | Cách kiểm chứng |
| --- | --- | --- | --- |
| NFR-MAINT-01 | Clean Architecture: Domain, Application, Infrastructure, Presentation | Xem layering.md | Review hướng tham chiếu giữa các project |
| NFR-MAINT-02 | Độ bao phủ unit và integration test tối thiểu 80% cho ledger, giữ hàng, giá vốn | Đo theo dòng lệnh, gộp unit và integration, riêng ba module Inventory, Reservation, Costing | Báo cáo coverlet trong CI |

---

## 5. Yêu cầu giao diện người dùng (UI/UX)

### 5.1 Màn hình Tổng quan (Dashboard)

**Mục tiêu:** Cung cấp cái nhìn nhanh về tình hình kinh doanh và tồn kho

**Thành phần giao diện:**

1. **Header Section**
   - Tiêu đề: "Tổng quan"
   - Chào người dùng: "Xin chào, [Tên người dùng]"
   - Nút refresh dữ liệu

2. **KPI Cards (4 cards)**
   - Doanh thu hôm nay: số tiền, % so với hôm qua (xanh tăng, đỏ giảm)
   - Số đơn hôm nay: số lượng, % so với hôm qua
   - Sản phẩm sắp hết hàng: số lượng SKU dưới ngưỡng
   - Đơn chờ duyệt: số lượng đơn status Reserved

3. **Biểu đồ doanh thu**
   - Line chart: Doanh thu 7 ngày gần nhất
   - Bar chart: Doanh thu theo kênh (POS, Shopee, TikTok, Lazada)
   - Filter: 7 ngày, 30 ngày, 90 ngày

4. **Bảng đơn chờ duyệt**
   - Hiển thị 10 đơn gần nhất status Reserved
   - Cột: Mã đơn, Kênh, Chi nhánh, Tổng tiền, Giữ hàng tới
   - Auto refresh mỗi 60 giây

5. **Bảng cảnh báo tồn thấp**
   - Hiển thị 10 SKU có available <= threshold
   - Cột: SKU, Tên, Chi nhánh, Khả dụng, Ngưỡng
   - Auto refresh mỗi 60 giây

6. **Bảng sản phẩm bán chạy**
   - Top 5 sản phẩm bán chạy nhất 7 ngày
   - Cột: Tên sản phẩm, Số lượng bán, Doanh thu

**Responsive:**
- Desktop: 4 KPI cards ngang, chart full width, 2 bảng bên cạnh
- Tablet: 2 KPI cards mỗi hàng, chart thu nhỏ, bảng xếp dọc
- Mobile: 1 KPI card mỗi hàng, chart scroll ngang, bảng xếp dọc

### 5.2 Màn hình POS

**Mục tiêu:** Bán hàng nhanh tại quầy, tối ưu cho tốc độ

**Thành phần giao diện:**

1. **Header**
   - Logo "OISM POS"
   - Dropdown chọn chi nhánh
   - Tên người dùng, nút đăng xuất
   - Indicator online/offline

2. **Thanh tìm kiếm**
   - Input lớn, placeholder "Quét mã vạch hoặc tìm sản phẩm"
   - Auto-focus (desktop)
   - Grid sản phẩm với hình ảnh, tên, giá, tồn khả dụng

3. **Giỏ hàng**
   - Danh sách items với số lượng, giá, tổng tiền
   - Nút +/- thay đổi số lượng
   - Tổng số lượng và tổng tiền

4. **Thanh thanh toán (fixed bottom)**
   - Tổng tiền lớn
   - Chọn phương thức: Cash / QR
   - Nút "Thanh toán" lớn
   - Disabled khi giỏ trống hoặc mất mạng

5. **Phím tắt**
   - F2: Focus ô tìm kiếm
   - F4: Đổi phương thức thanh toán
   - Enter: Thêm SKU / Thanh toán
   - Escape: Xóa text / Xóa giỏ

### 5.3 Màn hình Đơn hàng

**Mục tiêu:** Quản lý đơn hàng từ mọi kênh (POS, Shopee, TikTok, Lazada)

**Thành phần giao diện:**

1. **Header**
   - Tiêu đề: "Đơn hàng"
   - Nút "Tạo đơn" (primary button)
   - Nút "Tải lại"

2. **Bộ lọc**
   - Dropdown Trạng thái: Tất cả, Reserved, Confirmed, Completed, Cancelled
   - Dropdown Kênh: Tất cả, POS, Shopee, TikTok Shop, Lazada
   - Dropdown Chi nhánh: Tất cả, [danh sách chi nhánh]
   - Range picker: Ngày tạo đơn
   - Nút "Reset bộ lọc"

3. **Bảng đơn hàng**
   - Cột: Mã đơn (link), Kênh (tag màu), Mã đơn sàn, Chi nhánh, Số dòng, Tổng tiền, Trạng thái (tag màu), Giữ hàng tới, Tạo lúc
   - Pagination: 20 items/trang
   - Row selection: Checkbox để batch action
   - Sortable: Mã đơn, Tổng tiền, Tạo lúc

4. **Batch Actions**
   - Floating bar khi chọn nhiều đơn
   - Actions: Duyệt tất cả, Hủy tất cả

5. **Chi tiết đơn hàng (Modal)**
   - Thông tin khách hàng
   - Danh sách items: SKU, tên, số lượng, giá, tổng
   - Tổng tiền
   - Phương thức thanh toán
   - Địa chỉ giao hàng (nếu đơn online)
   - Timeline trạng thái
   - Actions: Duyệt, Hủy, Hoàn tất

**Responsive:**
- Desktop: Full table
- Tablet: Table scroll ngang
- Mobile: Compact table, expandable row để xem chi tiết

### 5.4 Màn hình Sản phẩm

**Mục tiêu:** Quản lý danh sách sản phẩm và SKU

**Thành phần giao diện:**

1. **Header**
   - Tiêu đề: "Sản phẩm"
   - Nút "Thêm sản phẩm" (primary button)

2. **Bộ lọc**
   - Search input: "Tìm theo tên hoặc mã SKU"
   - Dropdown Danh mục: Tree select phân cấp
   - Dropdown Thương hiệu
   - Switch: Chỉ hiển thị đang bán

3. **Bảng sản phẩm**
   - Cột: Tên sản phẩm (link), Danh mục, Thương hiệu, Loại (đơn/có biến thể), Số SKU, Trạng thái
   - Pagination: 20 items/trang
   - Sortable: Tên, Số SKU

4. **Modal thêm sản phẩm**
   - Form: Tên, Danh mục, Thương hiệu, Mô tả
   - Switch: Có biến thể?
   - Nếu sản phẩm đơn: 1 SKU với mã, giá lẻ, giá sỉ
   - Nếu có biến thể: List SKU với mã, thuộc tính, giá
   - Switch: Đang bán / Ngừng bán

5. **Chi tiết sản phẩm (trang riêng)**
   - Thông tin sản phẩm
   - Danh sách SKU với mã, thuộc tính, giá, mã vạch
   - Nút thêm mã vạch, xóa SKU
   - Nút: Lưu thay đổi, Quay lại

**Responsive:**
- Desktop: Full table
- Tablet: Table scroll ngang
- Mobile: Card view (mỗi sản phẩm là 1 card)

### 5.5 Màn hình Tồn kho

**Mục tiêu:** Xem tồn kho theo chi nhánh và SKU, cảnh báo tồn thấp

**Thành phần giao diện:**

1. **Header**
   - Tiêu đề: "Tồn kho"

2. **Bộ lọc**
   - Dropdown Chi nhánh: Tất cả, [danh sách chi nhánh]
   - Search input: "Tìm theo mã hoặc tên SKU"
   - Switch: Chỉ hiển thị dưới ngưỡng

3. **Bảng tồn kho**
   - Cột: Mã SKU, Tên, Chi nhánh, Tồn (on_hand), Đang giữ (reserved), Khả dụng (available), Giá vốn (chỉ Owner), Ngưỡng, Actions
   - Highlight row khi available <= threshold (background light red)
   - Color badge cho available: Green (>threshold), Orange (<=threshold), Red (=0)
   - Progress bar: available / on_hand
   - Pagination: 20 items/trang
   - Sortable: Mã SKU, Tồn, Khả dụng

4. **Modal đặt ngưỡng**
   - Input number: Ngưỡng (min=0)
   - Helper text: "Để trống thì không sinh cảnh báo"
   - Nút: Lưu, Hủy

**Responsive:**
- Desktop: Full table
- Tablet: Table scroll ngang
- Mobile: Card view với color indicators và progress bar

### 5.6 Các màn hình khác

Các màn hình khác (Báo cáo, Kênh sàn, v.v.) tuân theo:
- Layout: Header + Content với sidebar navigation
- Components: Ant Design (Table, Form, Modal, Card)
- Responsive: Desktop/Tablet/Mobile
- Loading: Skeleton loader
- Empty: Icon + text thông báo
- Error: Alert banner với nút thử lại

---

## 6. Mô hình dữ liệu

Hệ thống sử dụng PostgreSQL với kiến trúc multi-tenant. Database `oism` có 5 schema: `identity`, `catalog`, `core`, `channel`, `insights`.

### 6.1 Schema identity

Quản lý tenant, người dùng, refresh token và chi nhánh.

**Bảng chính:**

| Bảng | Mô tả | Cột chính |
| --- | --- | --- |
| `tenants` | Thông tin tenant (khách hàng) | `id`, `name`, `status`, `created_at` |
| `users` | Người dùng của tenant | `id`, `tenant_id`, `full_name`, `email`, `phone`, `password_hash`, `role`, `branch_id`, `is_active` |
| `refresh_tokens` | Refresh token cho làm mới access token | `id`, `tenant_id`, `user_id`, `token_hash`, `expires_at`, `revoked_at` |
| `branches` | Chi nhánh/kho của tenant | `id`, `tenant_id`, `code`, `name`, `type`, `address`, `is_active`, `version` |

**Quan hệ:**
- `tenants` 1:N `users`
- `tenants` 1:N `branches`
- `users` 1:N `refresh_tokens`
- `branches` N:1 `users` (Cashier gắn với chi nhánh)

### 6.2 Schema catalog

Quản lý danh mục, thương hiệu, sản phẩm, SKU, mã vạch và giá niêm yết.

**Bảng chính:**

| Bảng | Mô tả | Cột chính |
| --- | --- | --- |
| `categories` | Danh mục phân cấp | `id`, `tenant_id`, `parent_id`, `name`, `sort_order` |
| `brands` | Thương hiệu | `id`, `tenant_id`, `name` |
| `products` | Sản phẩm | `id`, `tenant_id`, `category_id`, `brand_id`, `name`, `description`, `has_variants`, `is_active` |
| `skus` | SKU (Stock Keeping Unit) | `id`, `tenant_id`, `product_id`, `sku_code`, `attributes`, `retail_price`, `wholesale_price`, `is_active`, `version` |
| `barcodes` | Mã vạch của SKU | `id`, `tenant_id`, `sku_id`, `code`, `symbology` |

**Quan hệ:**
- `categories` 1:N `categories` (cây phân cấp)
- `categories` 1:N `products`
- `brands` 1:N `products`
- `products` 1:N `skus`
- `skus` 1:N `barcodes`

### 6.3 Schema core

Quản lý số dư tồn, sổ giao dịch, chứng từ kho và đơn hàng. Đây là schema quan trọng nhất.

**Bảng số dư và sổ:**

| Bảng | Mô tả | Cột chính |
| --- | --- | --- |
| `inventory_balances` | Số dư tồn theo SKU tại chi nhánh | `tenant_id`, `branch_id`, `sku_id`, `on_hand`, `reserved`, `avg_cost`, `reorder_threshold`, `version` |
| `inventory_transactions` | Sổ giao dịch chỉ thêm mới | `seq`, `id`, `tenant_id`, `branch_id`, `sku_id`, `type`, `reason`, `quantity`, `balance_after`, `unit_cost`, `reference_type`, `reference_id` |

**Bảng đơn hàng:**

| Bảng | Mô tả | Cột chính |
| --- | --- | --- |
| `orders` | Đơn hàng | `id`, `tenant_id`, `branch_id`, `order_number`, `channel`, `external_order_id`, `status`, `total_amount`, `reserved_until` |
| `order_items` | Chi tiết đơn hàng | `id`, `tenant_id`, `order_id`, `sku_id`, `sku_code`, `sku_name`, `quantity`, `unit_price`, `discount`, `cost_price` |
| `reservations` | Phần giữ hàng | `id`, `tenant_id`, `order_id`, `order_item_id`, `branch_id`, `sku_id`, `quantity`, `status`, `expires_at` |
| `payments` | Thanh toán (chỉ POS) | `id`, `tenant_id`, `order_id`, `method`, `amount`, `confirmed_by`, `confirmed_at` |

**Bảng chứng từ kho:**

| Bảng | Mô tả | Cột chính |
| --- | --- | --- |
| `suppliers` | Nhà cung cấp | `id`, `tenant_id`, `name`, `phone`, `is_active` |
| `purchase_receipts` | Phiếu nhập | `id`, `tenant_id`, `branch_id`, `supplier_id`, `receipt_number`, `status`, `note`, `confirmed_at` |
| `purchase_receipt_items` | Chi tiết phiếu nhập | `id`, `tenant_id`, `receipt_id`, `sku_id`, `quantity`, `unit_cost` |
| `stock_transfers` | Phiếu chuyển kho | `id`, `tenant_id`, `from_branch_id`, `to_branch_id`, `transfer_number`, `status`, `shipped_at`, `received_at` |
| `stock_transfer_items` | Chi tiết chuyển kho | `id`, `tenant_id`, `transfer_id`, `sku_id`, `quantity`, `unit_cost` |
| `stocktakes` | Phiếu kiểm kê | `id`, `tenant_id`, `branch_id`, `status`, `created_by`, `posted_at` |
| `stocktake_items` | Chi tiết kiểm kê | `id`, `tenant_id`, `stocktake_id`, `sku_id`, `counted_qty`, `system_qty`, `difference` |

**Bảng tham chiếu (dự từ event):**

| Bảng | Mô tả | Cột chính |
| --- | --- | --- |
| `sku_refs` | Bản sao SKU từ catalog | `tenant_id`, `sku_id`, `sku_code`, `name`, `barcodes`, `retail_price`, `wholesale_price`, `is_active`, `version` |
| `branch_refs` | Bản sao chi nhánh từ identity | `tenant_id`, `branch_id`, `code`, `name`, `type`, `is_active`, `version` |

**Bảng outbox/inbox:**
- `outbox_messages`: Event gửi đi (để đảm bảo consistency)
- `inbox_messages`: Event nhận đi (để xử lý idempotent)

### 6.4 Schema channel

Quản lý cấu hình shop sàn TMĐT và nhật ký webhook.

**Bảng chính:**

| Bảng | Mô tả | Cột chính |
| --- | --- | --- |
| `channel_shops` | Cấu hình shop sàn | `id`, `tenant_id`, `channel`, `shop_id`, `name`, `access_token`, `is_active` |
| `webhook_events` | Nhật ký webhook nhận | `id`, `tenant_id`, `channel`, `event_type`, `payload`, `received_at`, `processed_at`, `error` |

### 6.5 Schema insights

Quản lý dữ liệu báo cáo và dự báo.

**Bảng chính:**

| Bảng | Mô tả | Cột chính |
| --- | --- | --- |
| `daily_aggregates` | Tổng hợp dữ liệu theo ngày | `tenant_id`, `branch_id`, `date`, `revenue`, `cost`, `quantity`, `order_count` |
| `sales_facts` | Dữ liệu bán hàng chi tiết | `tenant_id`, `branch_id`, `sku_id`, `date`, `channel`, `revenue`, `cost`, `quantity` |
| `stock_snapshots` | Snapshot tồn kho | `tenant_id`, `branch_id`, `sku_id`, `date`, `on_hand`, `avg_cost` |
| `sku_names` | Tên SKU để báo cáo | `tenant_id`, `sku_id`, `name` |
| `forecast_runs` | Lịch sử chạy dự báo | `id`, `tenant_id`, `run_at`, `model_type`, `parameters` |
| `forecast_results` | Kết quả dự báo | `id`, `tenant_id`, `forecast_run_id`, `branch_id`, `sku_id`, `forecast_date`, `predicted_quantity` |

---

## 7. Giao diện API

### 7.1 Quy ước chung

**Base URL:** `http://localhost:8080/api/<service>/...`

**Xác thực:**
- Header `Authorization: Bearer <access token>` cho mọi endpoint (trừ endpoint công khai)
- `tenantId` lấy từ token, không gửi trong URL/body
- Access token: 60 phút
- Refresh token: 7 ngày

**Định dạng dữ liệu:**
- JSON, tên trường camelCase
- Thời gian: ISO 8601 UTC (ví dụ: `2026-10-20T03:15:27Z`)
- Tiền: số thập phân (numeric)
- Số lượng: số nguyên (integer)
- Phân trang: `{ "page": 1, "pageSize": 20 }` → `{ "items": [], "page": 1, "pageSize": 20, "total": 0 }`

**Xử lý lỗi:**
Lỗi trả theo ProblemDetails với trường `code`:

| Mã HTTP | `code` | Mô tả |
| --- | --- | --- |
| 400 | `validation_failed` | Dữ liệu vào sai |
| 401 | `unauthenticated` | Thiếu token, token hết hạn |
| 403 | `forbidden` | Không đủ quyền |
| 404 | `not_found` | Không tìm thấy |
| 409 | `duplicate` | Trùng mã |
| 409 | `insufficient_stock` | Không đủ tồn khả dụng |
| 409 | `invalid_state_transition` | Bước chuyển trạng thái sai |

### 7.2 API Identity

**Endpoint công khai:**
- `POST /api/identity/tenants` - Đăng ký tenant
- `POST /api/identity/auth/login` - Đăng nhập
- `POST /api/identity/auth/refresh` - Làm mới token

**Endpoint cần xác thực:**
- `POST /api/identity/auth/logout` - Đăng xuất
- `GET /api/identity/users` - Danh sách người dùng
- `POST /api/identity/users` - Tạo người dùng
- `PUT /api/identity/users/{id}` - Sửa người dùng
- `GET /api/identity/branches` - Danh sách chi nhánh
- `POST /api/identity/branches` - Tạo chi nhánh
- `PUT /api/identity/branches/{id}` - Sửa chi nhánh

### 7.3 API Catalog

- `GET /api/catalog/categories` - Danh sách danh mục
- `POST /api/catalog/categories` - Tạo danh mục
- `PUT /api/catalog/categories/{id}` - Sửa danh mục
- `DELETE /api/catalog/categories/{id}` - Xóa danh mục
- `GET /api/catalog/brands` - Danh sách thương hiệu
- `POST /api/catalog/brands` - Tạo thương hiệu
- `GET /api/catalog/products` - Danh sách sản phẩm
- `POST /api/catalog/products` - Tạo sản phẩm
- `GET /api/catalog/products/{id}` - Chi tiết sản phẩm
- `PUT /api/catalog/products/{id}` - Sửa sản phẩm
- `GET /api/catalog/skus` - Danh sách SKU
- `POST /api/catalog/skus` - Tạo SKU
- `PUT /api/catalog/skus/{id}` - Sửa SKU
- `POST /api/catalog/skus/{id}/barcodes` - Thêm mã vạch
- `DELETE /api/catalog/barcodes/{id}` - Xóa mã vạch

### 7.4 API Core

**Tồn kho:**
- `GET /api/core/stock` - Danh sách tồn kho
- `GET /api/core/stock/ledger` - Sổ giao dịch
- `POST /api/core/stock/threshold` - Đặt ngưỡng tồn

**Phiếu nhập:**
- `GET /api/core/purchase-receipts` - Danh sách phiếu nhập
- `POST /api/core/purchase-receipts` - Tạo phiếu nhập (Draft)
- `PUT /api/core/purchase-receipts/{id}/confirm` - Xác nhận phiếu nhập

**Chuyển kho:**
- `GET /api/core/stock-transfers` - Danh sách chuyển kho
- `POST /api/core/stock-transfers` - Tạo chuyển kho (Draft)
- `PUT /api/core/stock-transfers/{id}/ship` - Xuất kho
- `PUT /api/core/stock-transfers/{id}/receive` - Nhập kho

**Kiểm kê:**
- `GET /api/core/stocktakes` - Danh sách kiểm kê
- `POST /api/core/stocktakes` - Tạo kiểm kê
- `PUT /api/core/stocktakes/{id}/post` - Chốt kiểm kê

**Đơn hàng:**
- `GET /api/core/orders` - Danh sách đơn hàng
- `POST /api/core/orders` - Tạo đơn thủ công
- `GET /api/core/orders/{id}` - Chi tiết đơn hàng
- `PUT /api/core/orders/{id}/confirm` - Duyệt đơn
- `PUT /api/core/orders/{id}/complete` - Hoàn tất đơn
- `PUT /api/core/orders/{id}/cancel` - Hủy đơn

**POS:**
- `POST /api/core/pos/checkout` - Thanh toán POS (cần header `Idempotency-Key`)
- `GET /api/core/pos/sku-search` - Tìm SKU kèm tồn khả dụng

### 7.5 API Channel

- `GET /api/channel/shops` - Danh sách shop sàn
- `POST /api/channel/shops` - Thêm shop sàn
- `PUT /api/channel/shops/{id}` - Sửa shop sàn
- `GET /api/channel/webhook-events` - Nhật ký webhook
- `POST /api/channel/simulate` - Bắn webhook giả lập

### 7.6 API Insights

**Báo cáo:**
- `GET /api/insights/reports/gross-profit` - Báo cáo lợi nhuận gộp
- `GET /api/insights/reports/inventory` - Báo cáo tồn kho
- `GET /api/insights/alerts/low-stock` - Cảnh báo tồn thấp

**Dự báo:**
- `POST /api/insights/forecast/run` - Chạy dự báo
- `GET /api/insights/forecast/suggestions` - Đề xuất nhập hàng
- `POST /api/insights/forecast/backtest` - Backtest dự báo

---

## 8. Yêu cầu phần cứng và phần mềm

### 8.1 Môi trường phát triển

**Yêu cầu tối thiểu:**
- CPU: 4 cores
- RAM: 8 GB
- Disk: 20 GB SSD
- OS: Windows 10/11, macOS, hoặc Linux (Ubuntu 20.04+)

**Phần mềm cần thiết:**
- .NET 8 SDK
- Node.js 18+
- PostgreSQL 15+
- RabbitMQ 3.12+
- Docker Desktop (cho Compose dev)
- Git

### 8.2 Môi trường sản xuất

**Yêu cầu đề xuất:**
- CPU: 8 cores
- RAM: 16 GB
- Disk: 100 GB SSD
- OS: Linux (Ubuntu 22.04 LTS)

**Phần mềm:**
- .NET 8 Runtime
- PostgreSQL 15+
- RabbitMQ 3.12+
- Nginx (reverse proxy)
- Docker & Docker Compose
- Hangfire (job scheduler)

### 8.3 Yêu cầu mạng

- HTTPS TLS 1.3
- Port 443 cho HTTPS
- Port 5671 cho RabbitMQ TLS
- Port 5432 cho PostgreSQL (nội bộ)

### 8.4 Yêu cầu backup

- Database backup hàng ngày
- Retention: 30 ngày
- Offsite backup: 1 tuần/lần

---

## 9. Phụ lục

### 9.1 Thuật ngữ

Xem mục 1.3 Định nghĩa

### 9.2 Danh sách Use Case

| Use Case | Mô tả | Mã yêu cầu liên quan |
| --- | --- | --- |
| UC-AUTH-01 | Đăng ký tenant | FR-AUTH-01 |
| UC-AUTH-02 | Đăng nhập/đăng xuất | FR-AUTH-02 |
| UC-AUTH-03 | Phân quyền theo vai trò | FR-AUTH-03 |
| UC-AUTH-04 | Quản lý chi nhánh | FR-AUTH-04 |
| UC-PROD-01 | Quản lý danh mục và thương hiệu | FR-PROD-01 |
| UC-PROD-02 | Quản lý sản phẩm và SKU | FR-PROD-02 |
| UC-PROD-03 | Quản lý mã vạch | FR-PROD-03 |
| UC-PROD-04 | Quản lý giá | FR-PROD-04 |
| UC-INV-01 | Ghi sổ giao dịch tồn kho | FR-INV-01 |
| UC-INV-02 | Xử lý phiếu nhập | FR-INV-02, FR-COST-01 |
| UC-INV-03 | Chuyển kho | FR-INV-03 |
| UC-INV-04 | Kiểm kê | FR-INV-04 |
| UC-ORD-01 | Tạo đơn từ POS | FR-ORD-01, FR-POS-02 |
| UC-ORD-02 | Tạo đơn thủ công | FR-ORD-01 |
| UC-ORD-03 | Duyệt đơn | FR-ORD-03, FR-RSE-03 |
| UC-ORD-04 | Hoàn tất đơn | FR-ORD-03, FR-RSE-03, FR-COST-02 |
| UC-ORD-05 | Hủy đơn | FR-ORD-04, FR-RSE-03 |
| UC-ORD-06 | Hết hạn giữ hàng | FR-ORD-05 |
| UC-POS-01 | Tìm và chọn sản phẩm | FR-POS-01, FR-POS-02 |
| UC-POS-02 | Thanh toán POS | FR-POS-02, FR-POS-04, FR-COST-02 |
| UC-POS-03 | In hóa đơn | FR-POS-03 |
| UC-REP-01 | Xem báo cáo lợi nhuận | FR-REP-01 |
| UC-REP-02 | Xem báo cáo tồn | FR-REP-02 |
| UC-REP-03 | Xem cảnh báo tồn thấp | FR-REP-03 |
| UC-INV-05 | Đặt ngưỡng tồn | FR-REP-03 |
| UC-SIM-01 | Bắn webhook giả lập | FR-SIM-01 |
| UC-SIM-02 | Nhận thông báo realtime | FR-SIM-02 |
| UC-AI-01 | Xem đề xuất nhập hàng | FR-AI-02 |
| UC-AI-02 | Chạy dự báo và backtest | FR-AI-01, FR-AI-03 |

### 6.3 Tham chiếu tài liệu

- `docs/requirements/functional.md` - Yêu cầu chức năng chi tiết
- `docs/requirements/non-functional.md` - Yêu cầu phi chức năng chi tiết
- `docs/usecase-userstory/` - Use case và user story
- `docs/architecture/` - Kiến trúc hệ thống
- `docs/design/` - Thiết kế chi tiết (data model, API, event)
- `docs/decisions/` - Các quyết định thiết kế
- `docs/PLAN.md` - Kế hoạch triển khai

---

## Lịch sử thay đổi

| Ngày | Phiên bản | Thay đổi | Người thực hiện |
| --- | --- | --- | --- |
| 15/10/2026 | 1.0 | Tạo tài liệu SRS ban đầu dựa trên requirements hiện có | Frontend Team |
