# ADR-0005: Confirmed là lúc xuất kho; hủy chỉ trước Confirmed; mỗi bước một transaction

- Trạng thái: Đã xác nhận 2026-10-04
- Gộp từ: D-05, D-06, D-07, D-08
- Ngày: 2026-10-03

## Bối cảnh

Đề để ngỏ bốn điểm quanh vòng đời đơn:

- FR-COST-02 chốt giá vốn "khi xuất hàng" nhưng lấy mốc Confirmed, trong khi FR-ORD-03 gọi đó là "duyệt đơn".
- Quy tắc Cancelled chỉ nói giảm `reserved`; áp sau Confirmed sẽ giảm hai lần và không xử lý được hàng đã xuất.
- NFR-SEC-02 đòi tạo đơn, giữ, trừ trong một transaction, trong khi đơn online phải nằm chờ ở Reserved.
- Đề không nêu thời hạn giữ hàng, cũng không nói đơn nhiều SKU thiếu một SKU thì xử lý thế nào.

## Quyết định

- Confirmed là thời điểm ghi nhận xuất kho: giảm `on_hand`, giảm `reserved`, chốt `CostPrice`, ghi ledger OUT. Completed nghĩa là đã giao xong và không tác động tồn.
- Chỉ hủy được từ Draft hoặc Reserved. Sau Confirmed không hủy; trả hàng và hoàn tiền nằm ngoài phạm vi.
- POS: một transaction cho cả chuỗi tạo đơn, giữ, xác nhận, thanh toán, hoàn tất.
- Đơn online và đơn thủ công: mỗi bước một transaction nguyên tử. Bước một là tạo đơn kèm giữ hàng; bước hai là xác nhận kèm trừ tồn; hoặc hủy, hết hạn kèm giải phóng.
- Phần giữ hàng hết hạn sau 30 phút, cấu hình được. Job Hangfire quét mỗi phút.
- Đơn nhiều SKU: giữ được toàn bộ hoặc từ chối toàn bộ.

Trạng thái và bước chuyển ở [design/state-machines.md](../design/state-machines.md).

## Hệ quả

- Doanh thu và giá vốn của báo cáo lấy theo mốc `ConfirmedAt`.
- Không có đường nào đưa hàng đã xuất quay lại tồn ngoài bút toán kiểm kê.
- Job hết hạn và thao tác xác nhận tranh nhau trên cùng một đơn; ai khóa được dòng đơn trước thì thắng, bên còn lại thấy trạng thái đã đổi và dừng.
- Một SKU thiếu hàng làm cả đơn bị từ chối, kể cả khi các SKU khác còn đủ.

## Phương án đã loại

- Thêm trạng thái Shipped tách khỏi Confirmed: đúng nghiệp vụ hơn, nhưng đề đã cố định bốn trạng thái ở FR-ORD-02.
- Giữ một transaction mở suốt vòng đời đơn online: không làm được, vì giữa các bước có người thao tác.
- Giữ một phần đơn khi thiếu hàng: cần thêm trạng thái giữ một phần và quy tắc bù hàng.

## Nếu bị đổi

- Thêm trạng thái xuất kho riêng: dời trừ tồn và chốt giá vốn khỏi bước xác nhận; sửa máy trạng thái, W3-01, công thức báo cáo.
- Bắt buộc có trả hàng: thêm luồng `ReturnIn` nối với dòng đơn gốc, khoảng 3 ngày công.
- Đổi thời hạn giữ hàng: chỉ đổi cấu hình.
