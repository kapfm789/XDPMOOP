# ADR-0008: Công thức và mốc thời gian của báo cáo

- Trạng thái: Đã xác nhận 2026-10-04
- Gộp từ: D-12
- Ngày: 2026-10-03

## Bối cảnh

FR-REP-01 dùng chữ "doanh thu thuần" và công thức lợi nhuận gộp nhưng không nói báo cáo lấy đơn ở trạng thái nào, có trừ chiết khấu, thuế, phí hay không. FR-REP-02 không định nghĩa "bán chạy", "bán chậm". Đề cũng không nói múi giờ.

## Quyết định

| Chỉ tiêu | Công thức |
| --- | --- |
| Doanh thu thuần | Tổng `quantity × unit_price − discount` của các dòng đơn đã Confirmed hoặc Completed |
| Giá vốn hàng bán | Tổng `quantity × cost_price` của cùng các dòng đó |
| Lợi nhuận gộp | Doanh thu thuần trừ giá vốn hàng bán |
| Giá trị tồn | Tổng `on_hand × avg_cost` theo chi nhánh và SKU |
| Bán chạy | Xếp giảm dần theo số lượng bán trong 30 ngày gần nhất |
| Bán chậm | SKU còn tồn, xếp tăng dần theo số lượng bán trong 30 ngày gần nhất |
| Cảnh báo tồn | `available <= reorder_threshold` của SKU tại chi nhánh |

- Mốc ghi nhận là `ConfirmedAt` của đơn.
- Không tính thuế, phí vận chuyển, phí sàn. Không có hàng trả vì trả hàng nằm ngoài phạm vi.
- Ngày báo cáo cắt theo giờ Việt Nam (UTC+7); dữ liệu lưu UTC.
- Ngưỡng tồn đặt theo từng chi nhánh và SKU. SKU chưa đặt ngưỡng thì không cảnh báo.

## Hệ quả

- Báo cáo đọc từ bảng `sales_facts` của `insights`, dựng từ event `OrderConfirmed` có sẵn giá vốn đã chốt. Giá vốn lịch sử không đổi khi có đợt nhập mới.
- Báo cáo trễ theo event, thường dưới vài giây.
- Vì không hủy được sau Confirmed, một dòng đã vào báo cáo không bao giờ bị rút ra.

## Phương án đã loại

- Ghi nhận theo Completed: hợp với kế toán hơn, nhưng đề gắn việc chốt giá vốn với Confirmed.
- Tính lại giá vốn theo giá hiện tại khi chạy báo cáo: làm lợi nhuận quá khứ đổi theo mỗi lần nhập hàng.

## Nếu bị đổi

Sửa công thức ở `insights` và ví dụ đối chiếu trong test T10; nếu thêm thuế hoặc phí thì thêm trường vào event `OrderConfirmed`.
