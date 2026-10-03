# ADR-0009: Chuyển kho nhận đủ; kiểm kê bị chặn khi số đếm thấp hơn lượng đã giữ

- Trạng thái: Đề xuất, chờ giảng viên xác nhận
- Gộp từ: D-13
- Ngày: 2026-10-03

## Bối cảnh

FR-INV-03 chỉ nói chuyển kho hai bước, không nói hàng đang vận chuyển theo dõi ở đâu, có nhận thiếu hay hủy được không. FR-INV-04 không nói phải làm gì khi số đếm thấp hơn lượng đã giữ: ví dụ `reserved = 8` mà đếm được 6, đặt `on_hand = 6` sẽ làm tồn khả dụng âm.

## Quyết định

Chuyển kho:

- Trạng thái Draft → InTransit → Received.
- Khi xuất: kiểm tồn khả dụng của nơi gửi, giảm `on_hand` nơi gửi, ghi ledger OUT với lý do TransferOut. Số lượng đang vận chuyển nằm trên phiếu chuyển kho, kèm đơn giá vốn của nơi gửi.
- Khi nhận: tăng `on_hand` nơi nhận, ghi ledger IN với lý do TransferIn, tính lại giá vốn bình quân nơi nhận.
- Nhận đủ số lượng đã xuất. Không nhận thiếu, không ghi hỏng.
- Chỉ xóa được phiếu khi còn Draft. Đã InTransit thì không hủy.

Kiểm kê:

- Trạng thái Open → Posted. Số trên sổ được chụp lúc chốt, dưới khóa dòng số dư.
- Nếu số đếm của một SKU nhỏ hơn `reserved`, cả phiên bị từ chối kèm danh sách đơn đang giữ SKU đó. Nhân viên hủy hoặc xử lý các đơn ấy rồi chốt lại.
- Chênh lệch ghi thành ledger IN hoặc OUT với lý do StocktakeAdjust; `avg_cost` giữ nguyên.

## Hệ quả

- Hàng đang vận chuyển không tính vào tồn khả dụng của bất kỳ chi nhánh nào cho tới khi nơi nhận xác nhận.
- Tổng tồn nơi gửi, hàng đang vận chuyển và tồn nơi nhận không đổi qua hai bước.
- Thiếu hàng thật khi đang có đơn giữ không bị che đi; con người phải quyết định đơn nào bị hủy.
- Hàng thiếu hoặc hỏng khi chuyển kho được xử lý bằng một phiên kiểm kê ở nơi nhận sau khi nhận đủ trên sổ.

## Phương án đã loại

- Tự hủy bớt đơn khi kiểm kê thiếu: hệ thống tự chọn khách bị hủy đơn là quyết định nghiệp vụ không nên tự động.
- Cho phép tồn khả dụng âm tạm thời: trái NFR-PERF-02.
- Nhận một phần khi chuyển kho: cần thêm bút toán chênh lệch và trạng thái nhận dở.

## Nếu bị đổi

Cho nhận thiếu: thêm số lượng thực nhận trên dòng phiếu và bút toán điều chỉnh ở nơi gửi hoặc tài khoản hao hụt; sửa W3-05 và bất biến số 9.
