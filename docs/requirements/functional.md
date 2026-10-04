# Yêu cầu chức năng

Đề có 30 yêu cầu chức năng thuộc 9 nhóm; nhóm thêm 3 mã tạm cho phần AI dự báo mà đề chỉ nêu trong gói công việc 5. Phát biểu dưới đây rút gọn từ Phần I của [file phân tích](../../OISM-ban-dich-va-phan-tich-tieng-Viet.md); khi cần nguyên văn thì đọc file đó.

Task và kịch bản kiểm chứng của từng mã nằm ở RTM, [PLAN.md](../PLAN.md) mục 16. Cột "Use case" trỏ tới [usecase-userstory/](../usecase-userstory/README.md).

## FR-AUTH: tenant và xác thực

| Mã | Yêu cầu | Use case | Service |
| --- | --- | --- | --- |
| FR-AUTH-01 | Tạo tenant mới; mỗi tenant có không gian dữ liệu riêng và một `TenantId` duy nhất | UC-AUTH-01 | identity |
| FR-AUTH-02 | Đăng nhập, đăng xuất bằng email hoặc số điện thoại kèm mật khẩu; trả JWT chứa `TenantId`, `UserId`, `Role` | UC-AUTH-02 | identity |
| FR-AUTH-03 | Kiểm soát truy cập API và màn hình theo vai trò: Owner toàn quyền, Staff quản lý kho và đơn, Cashier chỉ dùng POS | UC-AUTH-03 | identity, gateway, mọi service |
| FR-AUTH-04 | Owner tạo, sửa, bật hoặc tắt chi nhánh và kho | UC-AUTH-04 | identity |

## FR-PROD: sản phẩm và biến thể

| Mã | Yêu cầu | Use case | Service |
| --- | --- | --- | --- |
| FR-PROD-01 | Quản lý danh mục phân cấp và danh sách thương hiệu | UC-PROD-01 | catalog |
| FR-PROD-02 | Sản phẩm đơn và sản phẩm có biến thể (màu, cỡ); mỗi biến thể có mã SKU duy nhất trong tenant | UC-PROD-02 | catalog |
| FR-PROD-03 | Tự sinh hoặc nhập tay mã vạch EAN-13 hoặc Code128 cho từng SKU | UC-PROD-03 | catalog |
| FR-PROD-04 | Giá lẻ và giá sỉ cho từng SKU, tách khỏi giá vốn | UC-PROD-04 | catalog |

## FR-INV: sổ giao dịch tồn kho

| Mã | Yêu cầu | Use case | Service |
| --- | --- | --- | --- |
| FR-INV-01 | Mọi biến động tồn (nhập, xuất, bán, trả, kiểm kê, chuyển kho) ghi vào sổ `InventoryTransaction` chỉ thêm mới; không sửa trực tiếp cột tồn | UC-INV-01 | core |
| FR-INV-02 | Phiếu nhập từ nhà cung cấp làm tăng `on_hand` và cập nhật giá vốn bình quân | UC-INV-02 | core |
| FR-INV-03 | Chuyển kho hai bước: xuất vào trạng thái đang vận chuyển, rồi xác nhận nhập tại nơi nhận | UC-INV-03 | core |
| FR-INV-04 | Phiên kiểm kê: ghi số đếm, so với số trên sổ, tạo bút toán điều chỉnh | UC-INV-04 | core |

## FR-COST: giá vốn bình quân gia quyền

| Mã | Yêu cầu | Use case | Service |
| --- | --- | --- | --- |
| FR-COST-01 | Khi xác nhận phiếu nhập, tính lại giá vốn: (tồn cũ × giá vốn cũ + SL nhập × đơn giá nhập) / (tồn cũ + SL nhập) | UC-INV-02 | core |
| FR-COST-02 | Lưu cố định giá vốn vào `OrderItem.CostPrice` khi đơn sang Confirmed hoặc khi POS thanh toán xong | UC-ORD-03, UC-POS-02 | core |

## FR-ORD: trung tâm đơn hàng

| Mã | Yêu cầu | Use case | Service |
| --- | --- | --- | --- |
| FR-ORD-01 | Chuẩn hóa đơn từ POS, đơn thủ công trên admin và webhook giả lập về một Canonical Order Model | UC-ORD-01, UC-ORD-02, UC-POS-02 | core, channel |
| FR-ORD-02 | Máy trạng thái Draft → Reserved → Confirmed → Completed, hoặc Cancelled; chặn bước chuyển sai | UC-ORD-03 đến UC-ORD-06 | core |
| FR-ORD-03 | Nhân viên xem đơn chờ xử lý, duyệt, in phiếu giao hàng hoặc hủy | UC-ORD-03, UC-ORD-04, UC-ORD-06 | core, admin |

## FR-RSE: chống bán vượt tồn

| Mã | Yêu cầu | Use case | Service |
| --- | --- | --- | --- |
| FR-RSE-01 | Duy trì `available = on_hand - reserved` cho từng SKU tại từng chi nhánh | UC-INV-01 | core |
| FR-RSE-02 | Đơn online vào Reserved thì tăng `reserved`; từ chối đơn nếu `available` nhỏ hơn số lượng đặt | UC-ORD-01 | core |
| FR-RSE-03 | Sang Confirmed: giảm `on_hand`, giảm `reserved`, ghi ledger. Sang Cancelled: giảm `reserved` | UC-ORD-03, UC-ORD-04, UC-ORD-05 | core |

## FR-POS: bán tại quầy

| Mã | Yêu cầu | Use case | Service |
| --- | --- | --- | --- |
| FR-POS-01 | Giao diện bán nhanh cho cảm ứng và phím tắt; tìm theo tên, SKU, mã vạch | UC-POS-01 | pos |
| FR-POS-02 | Kiểm `available` ngay khi chọn sản phẩm; chặn bán vượt để bảo vệ hàng đã giữ cho đơn online | UC-POS-01, UC-POS-02 | core, pos |
| FR-POS-03 | Thanh toán tiền mặt hoặc chuyển khoản QR; mở hộp thoại in của trình duyệt | UC-POS-02, UC-POS-03 | pos |
| FR-POS-04 | Checkout POS chạy giữ hàng → xác nhận → trừ hàng trong một transaction nguyên tử | UC-POS-02 | core |

## FR-REP: báo cáo và cảnh báo

| Mã | Yêu cầu | Use case | Service |
| --- | --- | --- | --- |
| FR-REP-01 | Doanh thu thuần, giá vốn hàng bán, lợi nhuận gộp; lọc theo thời gian, chi nhánh, kênh, SKU | UC-REP-01 | insights |
| FR-REP-02 | Giá trị tồn theo giá vốn; hàng bán chạy và hàng bán chậm | UC-REP-02 | insights |
| FR-REP-03 | Cảnh báo trên dashboard khi `available ≤ Threshold` của SKU | UC-REP-03, UC-INV-05 | insights, core |

## FR-SIM: giả lập và thời gian thực

| Mã | Yêu cầu | Use case | Service |
| --- | --- | --- | --- |
| FR-SIM-01 | Công cụ gửi webhook giả lập Shopee, TikTok, Lazada để thử tải và cơ chế khóa | UC-SIM-01 | channel |
| FR-SIM-02 | SignalR đẩy thông báo đơn mới tới admin và POS mà không tải lại trang | UC-SIM-02 | insights, frontend |
| FR-SIM-03 | Hangfire tự hủy đơn giữ hàng đã hết hạn và tổng hợp báo cáo hằng ngày | UC-ORD-05, UC-REP-01 | core, insights |

## FR-AI: dự báo nhập hàng (mã tạm)

Đề chỉ ghi "tích hợp dự báo bằng AI để đề xuất nhập hàng bổ sung" ở gói công việc 5, không có mã. Ba mã dưới đây do nhóm đặt, giảng viên đã xác nhận ngày 2026-10-04 theo [ADR-0010](../decisions/0010-ai-forecast.md).

| Mã | Yêu cầu | Use case | Service |
| --- | --- | --- | --- |
| FR-AI-01 | Dự báo nhu cầu bán theo từng SKU tại từng chi nhánh từ lịch sử bán | UC-AI-02 | insights |
| FR-AI-02 | Hiển thị đề xuất số lượng nên nhập trên admin; chỉ là khuyến nghị, không tự tạo phiếu | UC-AI-01 | insights, admin |
| FR-AI-03 | Backtest dự báo trên lịch sử và báo cáo sai số so với một phương án cơ sở | UC-AI-02 | insights |
