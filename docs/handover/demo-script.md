# Kịch bản demo

Kịch bản buổi demo khoảng 20 phút, đi qua các luồng chính của đề trên gói demo có dữ liệu seed (task W5-09, mục f của đề). Mỗi bước ghi thao tác trên giao diện, điều người xem phải thấy và yêu cầu mà bước đó chứng minh.

Phần API của kịch bản có bản tự động: [demo.postman_collection.json](demo.postman_collection.json) gọi đúng các API mà giao diện gọi ở mỗi bước, kèm kiểm tra kết quả. Mỗi lần chạy tạo sản phẩm mới có hậu tố riêng nên chạy lặp lại được trên cùng dữ liệu. Đây là cách kiểm điều kiện "demo chạy hai lần liên tiếp không lỗi" trước buổi demo thật.

## Chuẩn bị

| Việc | Cách làm |
| --- | --- |
| Dựng gói demo | Theo [install-guide.md](install-guide.md); `seed` phải `Exited (0)` |
| Chạy thử phần API hai lần | Lệnh ở mục "Chạy thử tự động" dưới; cả hai lần phải xanh |
| Cửa sổ 1 | Trang quản trị http://localhost:5173, đăng nhập `owner@moc.test` |
| Cửa sổ 2 | POS http://localhost:5174, đăng nhập `cashier@moc.test`, đặt cạnh cửa sổ 1 để thấy thông báo realtime |
| Cửa sổ 3 (tùy chọn) | Trình duyệt ẩn danh, trang quản trị, để đăng nhập `staff@moc.test` và `owner@xanh.test` ở bước 10 và 12 |
| Âm thanh | Bật loa để nghe chuông khi có đơn mới |

Mật khẩu mọi tài khoản mẫu: `Demo@2026`. Thay `<hh>` ở dưới bằng một hậu tố chưa dùng, ví dụ giờ và phút lúc demo (`1430`), để mã SKU không trùng với lần chạy trước.

## Các bước

| # | Thao tác | Thấy gì | Chứng minh |
| --- | --- | --- | --- |
| 1 | Cửa sổ 1, **Tổng quan** | Thẻ doanh thu hôm nay, đơn chờ duyệt, SKU tồn thấp; biểu đồ doanh thu 7 ngày từ lịch sử seed; bảng cảnh báo tồn | FR-REP-01, FR-REP-03 |
| 2 | **Sản phẩm › Sản phẩm**, **Thêm sản phẩm** "Áo polo demo", có biến thể, hai SKU `POLO-<hh>-M` giá lẻ 150.000 và `POLO-<hh>-L` giá lẻ 160.000. Ở trang sản phẩm, **Thêm mã vạch** EAN-13 để trống mã cho SKU M | Sản phẩm có hai biến thể; mã vạch 13 số tự sinh, đúng số kiểm tra | FR-PROD-02, 03, 04 |
| 3 | **Kho › Phiếu nhập**, tạo phiếu cho Mộc Quận 1: `POLO-<hh>-M` 10 cái giá 100.000, `POLO-<hh>-L` 1 cái giá 90.000; **Xác nhận nhập kho**. Tạo phiếu thứ hai `POLO-<hh>-M` 5 cái giá 130.000, xác nhận | **Kho › Tồn kho**: SKU M tồn 15, giá vốn bình quân 110.000; **Xem sổ** có hai dòng nhập | FR-INV-01, FR-INV-02, FR-COST-01 (T09) |
| 4 | **Đơn hàng › Tạo đơn**: Mộc Quận 1, `POLO-<hh>-M` 2 cái. Ở chi tiết đơn, **Duyệt đơn**, rồi **Hoàn tất**, **In phiếu giao** | Thanh bước đi từ Giữ hàng tới Hoàn tất; dòng đơn có giá vốn 110.000; phiếu giao in được | FR-ORD-01, 02, 03, FR-RSE-03, FR-COST-02 |
| 5 | Cửa sổ 2 (POS): gõ mã vạch của SKU M rồi Enter, Enter lần nữa | Hàng vào giỏ ở lần Enter đầu; lần sau thanh toán tiền mặt và mở hóa đơn để in | FR-POS-01 đến 04, NFR-USA-02 |
| 6 | Cửa sổ 1, **Kênh sàn**, shop `moc-shopee`, **Bắn đơn giả lập**: mã `POLO-<hh>-M`, 1 đơn, đơn giá 150.000 | Popup và chuông "Đơn mới" ở trang quản trị và POS, không cần tải lại; **Webhook đã nhận** ghi Đã giữ hàng; mở đơn Shopee và **Duyệt đơn** | FR-SIM-01, FR-SIM-02, FR-ORD-01 |
| 7 | Flash sale: **Bắn đơn giả lập** mã `POLO-<hh>-L` (chỉ còn 1 cái), 50 đơn, 50 đơn cùng lúc | **Đơn hàng › Danh sách đơn** chỉ có 1 đơn Shopee chờ duyệt cho SKU L; **Webhook đã nhận** có 49 dòng Bị từ chối, lý do không đủ tồn; tồn SKU L: tồn 1, đang giữ 1, khả dụng 0 | FR-RSE-01, 02, NFR-PERF-02 (T01) |
| 8 | **Kho › Chuyển kho**: Mộc Quận 1 sang Mộc Quận 3, `POLO-<hh>-M` 2 cái; **Xuất kho**. Mở **Kho › Tồn kho** lọc Mộc Quận 3, rồi quay lại **Nhận hàng** | Trước khi nhận, Quận 3 chưa có SKU M để bán (khả dụng 0) dù Quận 1 đã trừ 2; nhận xong Quận 3 có 2 | FR-INV-03 (T12) |
| 9 | **Kho › Kiểm kê**: mở phiên ở Mộc Quận 3, đếm `POLO-<hh>-M` được 1, **Chốt** | Chênh lệch -1, sổ có dòng điều chỉnh kiểm kê | FR-INV-04 |
| 10 | **Báo cáo › Lợi nhuận gộp**, lọc SKU `POLO-<hh>-M`, khoảng hôm nay, nhóm theo kênh. Rồi cửa sổ 3 đăng nhập `staff@moc.test` | Doanh thu 600.000, giá vốn 440.000, lãi gộp 160.000 chia ba kênh Thủ công, POS, Shopee. Nhân viên không có mục Lợi nhuận gộp trong menu | FR-REP-01, FR-COST-02 (T10), FR-AUTH-03 |
| 11 | **Kho › Tồn kho**, **Đặt ngưỡng** 20 cho SKU M ở Quận 1. **Báo cáo › Dự báo nhập hàng**, **Chạy dự báo** | Popup "Tồn thấp" và cột Mức tồn chuyển "Sắp hết"; lần chạy dự báo thành công, có WAPE so với phương án cơ sở; SKU mới hiện "Chưa đủ dữ liệu" | FR-REP-03, FR-AI-01, 02, 03 |
| 12 | Cửa sổ 3 đăng nhập `owner@xanh.test`, mở **Đơn hàng › Danh sách đơn** | Chỉ thấy đơn của "Tạp hóa Xanh"; mở địa chỉ chi tiết đơn của Mộc thì bị đưa về danh sách | NFR-TENANT-01 (T14) |

Nếu còn thời gian: Swagger ở http://localhost:8080/swagger (bấm Authorize, gọi thử một API), và cài POS thành ứng dụng bằng biểu tượng cài ở thanh địa chỉ.

## Chạy thử tự động

Chạy phần API của kịch bản hai lần liên tiếp, từ gốc repo, khi gói demo đang chạy:

```bash
docker run --rm -v "$PWD/docs/handover:/etc/newman" postman/newman:6-alpine run demo.postman_collection.json --env-var baseUrl=http://host.docker.internal:8080 -n 2
```

`-n 2` chạy hai vòng liền nhau trong một lệnh. Mật khẩu seed khác `Demo@2026` thì thêm `--env-var password=<mật khẩu>`. Trên Linux không có Docker Desktop, thêm `--add-host host.docker.internal:host-gateway` sau `--rm`.

| Thư mục của collection | Bước của kịch bản |
| --- | --- |
| Bước 0. Chuẩn bị | Đăng nhập ba vai trò, lấy chi nhánh, danh mục, nhà cung cấp, shop từ seed |
| Bước 2. Sản phẩm | 2 |
| Bước 3. Nhập hàng và giá vốn | 3 |
| Bước 4. Đơn thủ công | 4 |
| Bước 5. Bán tại quầy | 5, thêm kiểm bấm thanh toán hai lần chỉ ra một đơn (T03) |
| Bước 6 và 7. Đơn sàn và flash sale | 6, 7 |
| Bước 8 và 9. Chuyển kho và kiểm kê | 8, 9 |
| Bước 10 và 11. Báo cáo, cảnh báo, dự báo | 10, 11 |
| Bước 12. Cách ly tenant | 12 |

Bước 1 và các phần chỉ có ở giao diện (popup, chuông, âm thanh, in, PWA) kiểm bằng mắt khi tập.

## Kết quả tập demo

Nhật ký các lần tập; mỗi lần ghi ngày, môi trường và kết quả.

| Ngày | Môi trường | Kết quả |
| --- | --- | --- |
| 2026-10-18 | Compose dev có seed, laptop i5-10200H, Docker 3,7 GB RAM | Phần API hai vòng liên tiếp: 119 request, 154 assertion, không lỗi |
| 2026-10-18 | Gói demo dựng lại từ nhánh phase 10, cùng máy | Phần API hai vòng liên tiếp: 108 request, 154 assertion, không lỗi; Postman collection API 141 assertion đạt; giao diện bản build nginx mở được tổng quan, danh sách đơn, chi tiết đơn, tồn kho |
