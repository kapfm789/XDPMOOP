# ADR-0003: Ledger chỉ thêm mới kèm bảng số dư, khóa bi quan trên dòng số dư

- Trạng thái: Đã xác nhận 2026-10-04
- Gộp từ: D-02, D-03
- Ngày: 2026-10-03

## Bối cảnh

FR-INV-01 cấm cập nhật trực tiếp cột tồn, nhưng FR-INV-02, FR-RSE-03 và NFR-PERF-02 lại nói tới tăng, giảm, cập nhật `on_hand` và `reserved`. Đề không nói tồn hiện tại được tính từ sổ hay được lưu riêng. NFR-PERF-02 cho chọn giữa khóa bi quan và khóa lạc quan.

## Quyết định

- Có bảng `inventory_balances` lưu `on_hand`, `reserved`, `avg_cost` cho từng tenant, chi nhánh, SKU.
- `on_hand` chỉ đổi trong cùng transaction với việc ghi đúng một dòng `inventory_transactions`, qua một hàm duy nhất `PostLedger`. Ledger là nguồn lịch sử chuẩn; số dư phải dựng lại được từ ledger.
- Cách đọc câu cấm của FR-INV-01: cấm sửa số dư ngoài nghiệp vụ ghi sổ, không cấm bảng số dư.
- Khóa bi quan `SELECT ... FOR UPDATE` trên dòng số dư, theo thứ tự chi nhánh rồi SKU.
- Hai hàng rào cuối ở database: CHECK `reserved <= on_hand` và trigger chặn `UPDATE`, `DELETE` trên ledger.

Chi tiết ở [architecture/transactions-and-concurrency.md](../architecture/transactions-and-concurrency.md).

## Hệ quả

- Đọc tồn và kiểm tồn khả dụng là đọc một dòng, không phải cộng cả sổ.
- Có đúng một đối tượng để khóa khi giữ hàng: dòng số dư.
- Phải có test đối soát: `on_hand` bằng tổng IN trừ tổng OUT; `reserved` bằng tổng phần giữ đang Active.
- Request tranh cùng một SKU xếp hàng chờ khóa; độ trễ tăng khi flash sale nhưng không bao giờ bán vượt.

## Phương án đã loại

- Không có bảng số dư, luôn tính tồn từ ledger: phải khóa bằng advisory lock hoặc khóa cả dải ledger, và mỗi lần kiểm tồn phải cộng toàn bộ sổ.
- Khóa lạc quan bằng cột phiên bản: dưới tải 50 request tranh một đơn vị, 49 request phải đọc lại và thử lại; code thử lại dễ sai hơn code chờ khóa.

## Nếu bị đổi

- Cấm bảng số dư: viết lại W2-01 và W2-05, đổi mục "Thứ tự khóa" và "Một cửa duy nhất" của tài liệu transaction.
- Chuyển sang khóa lạc quan: thêm cột `row_version`, thêm vòng thử lại trong `IStockService`, sửa test T01.
