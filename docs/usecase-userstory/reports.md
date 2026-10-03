# Use case: báo cáo, cảnh báo và dự báo

Năm use case của service `insights`. Thiết kế: [data model](../design/data-model/insights.md), [API](../design/api/insights.md), [thuật toán dự báo](../design/forecast.md), công thức ở [ADR-0008](../decisions/0008-report-formulas.md).

## UC-REP-01 Xem doanh thu và lợi nhuận gộp

- Actor: Owner
- Yêu cầu: FR-REP-01, FR-SIM-03, NFR-PERF-01
- API: `GET /api/insights/reports/gross-profit`

Là Owner, tôi muốn xem doanh thu, giá vốn và lợi nhuận gộp theo nhiều chiều, để biết kênh nào và mặt hàng nào đang sinh lời.

Tiêu chí chấp nhận:

1. Cho các đơn đã Confirmed trong khoảng thời gian, khi xem, thì báo cáo trả doanh thu thuần, giá vốn hàng bán và lợi nhuận gộp theo công thức của ADR-0008.
2. Cho đơn bán 2 sản phẩm giá 150.000 với giá vốn đã chốt 110.000, thì doanh thu 300.000, giá vốn 220.000, lợi nhuận gộp 80.000.
3. Cho bộ lọc thời gian, chi nhánh, kênh, SKU, khi kết hợp bất kỳ, thì kết quả chỉ gồm dòng khớp mọi điều kiện.
4. Cho đơn đã vào báo cáo, khi sau đó nhập lô mới làm đổi giá vốn hiện tại, thì số liệu của đơn cũ không đổi (T10).
5. Cho event `OrderConfirmed` tới hai lần, thì số liệu không bị cộng đôi (T22).
6. Cho Staff, khi gọi báo cáo này, thì trả 403.
7. Cho dưới 100.000 dòng bán, khi đo, thì báo cáo trả về dưới 2 giây (T17).
8. Job tổng hợp chạy mỗi đêm dựng bảng tổng hợp theo ngày; chạy lại job cho cùng ngày cho ra cùng kết quả.

## UC-REP-02 Xem giá trị tồn, hàng bán chạy, bán chậm

- Actor: Owner, Staff
- Yêu cầu: FR-REP-02
- API: `GET /api/insights/reports/inventory-value`, `/reports/top-sellers`, `/reports/slow-movers`

Là nhân viên, tôi muốn biết tiền đang nằm ở hàng nào và hàng nào bán nhanh hay chậm, để quyết định nhập và xả hàng.

Tiêu chí chấp nhận:

1. Cho tồn hiện tại, khi xem giá trị tồn, thì mỗi dòng là `on_hand × avg_cost` theo chi nhánh và SKU, kèm tổng.
2. Cho 30 ngày gần nhất, khi xem bán chạy, thì SKU xếp giảm dần theo số lượng bán.
3. Cho 30 ngày gần nhất, khi xem bán chậm, thì chỉ gồm SKU còn tồn, xếp tăng dần theo số lượng bán; SKU không bán được gì đứng đầu.

## UC-REP-03 Nhận cảnh báo tồn thấp

- Actor: Owner, Staff
- Yêu cầu: FR-REP-03
- API: `GET /api/insights/alerts/low-stock`, thông báo `LowStock` qua SignalR

Là nhân viên kho, tôi muốn được báo khi một SKU xuống tới ngưỡng, để kịp đặt hàng bổ sung.

Tiêu chí chấp nhận:

1. Cho SKU đã đặt ngưỡng, khi tồn khả dụng xuống bằng hoặc dưới ngưỡng, thì SKU xuất hiện trong danh sách cảnh báo và một thông báo được đẩy tới dashboard.
2. Cho SKU đang bị cảnh báo, khi tồn khả dụng lên trên ngưỡng, thì SKU rời danh sách.
3. Cho SKU chưa đặt ngưỡng, thì không bao giờ sinh cảnh báo.
4. Cảnh báo của một tenant không tới người dùng của tenant khác (T15).

## UC-AI-01 Xem đề xuất nhập hàng

- Actor: Owner
- Yêu cầu: FR-AI-01, FR-AI-02
- API: `GET /api/insights/forecast/reorder-suggestions`

Là Owner, tôi muốn xem hệ thống đề xuất nên nhập bao nhiêu cho từng SKU, để lên kế hoạch mua hàng.

Tiêu chí chấp nhận:

1. Cho lần chạy dự báo gần nhất, khi xem theo chi nhánh, thì mỗi dòng có SKU, nhu cầu dự báo 14 ngày, tồn khả dụng và số lượng đề xuất nhập.
2. Cho SKU có ít hơn 14 ngày lịch sử bán, thì dòng đó hiện "chưa đủ dữ liệu" thay cho con số.
3. Cho bất kỳ đề xuất nào, thì hệ thống không tự tạo phiếu nhập và không ghi sổ (T19).
4. Màn hình hiện thời điểm chạy dự báo gần nhất.
5. Cho lần chạy gần nhất bị lỗi, thì màn hình báo lỗi và vẫn hiện kết quả của lần chạy thành công trước đó.

## UC-AI-02 Chạy và đánh giá dự báo

- Actor: Owner, Hệ thống
- Yêu cầu: FR-AI-01, FR-AI-03
- API: `POST /api/insights/forecast/run`; job Hangfire mỗi đêm

Là Owner, tôi muốn biết dự báo đáng tin tới đâu, để không đặt hàng theo một con số vô căn cứ.

Tiêu chí chấp nhận:

1. Cho lịch sử bán, khi chạy dự báo, thì mỗi SKU tại mỗi chi nhánh có một kết quả và lần chạy được lưu lại với thời điểm, phương pháp và trạng thái.
2. Cho backtest, thì 14 ngày cuối bị giấu khỏi dữ liệu huấn luyện; không ngày nào sau mốc cắt được dùng để dự báo (T18).
3. Cho backtest, thì kết quả có sai số WAPE của dự báo và của phương án cơ sở là trung bình 7 ngày gần nhất.
4. Dự báo của một tenant chỉ dùng dữ liệu của tenant đó.
