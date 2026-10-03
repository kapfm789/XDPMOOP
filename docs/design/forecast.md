# Dự báo nhập hàng

Dự báo dùng san bằng hàm mũ đơn trên số lượng bán theo ngày của từng SKU tại từng chi nhánh, rồi đổi thành số lượng đề xuất nhập. Mọi tham số dưới đây là giá trị mặc định, đặt trong cấu hình của `insights`. Lý do chọn phương pháp ở [ADR-0010](../decisions/0010-ai-forecast.md).

## Đầu vào

- Nguồn: bảng `sales_facts`, gom theo ngày (giờ Việt Nam), chi nhánh, SKU.
- Cửa sổ: 60 ngày gần nhất tính tới hết ngày hôm qua.
- Ngày không có dòng bán nào được tính là 0.
- Tồn khả dụng hiện tại lấy từ `stock_snapshots`.

## Tham số

| Tham số | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `Alpha` | 0,3 | Hệ số san bằng; càng lớn càng bám dữ liệu gần |
| `HistoryDays` | 60 | Số ngày lịch sử dùng |
| `MinDaysWithHistory` | 14 | Ít hơn thì coi là chưa đủ dữ liệu |
| `HorizonDays` | 14 | Số ngày dự báo hiển thị |
| `LeadTimeDays` | 7 | Số ngày chờ hàng về |
| `SafetyDays` | 3 | Số ngày dự phòng |
| `BacktestDays` | 14 | Số ngày cuối giấu đi khi backtest |

## Thuật toán

Với mỗi chi nhánh và SKU, gọi `y(1)` tới `y(n)` là số lượng bán từng ngày, cũ trước mới sau.

```math
L_1 = y_1, \qquad L_t = \alpha \, y_t + (1 - \alpha) \, L_{t-1}
```

1. Số ngày kể từ ngày bán đầu tiên của SKU tại chi nhánh nhỏ hơn `MinDaysWithHistory` thì đặt `has_enough_data = false` và dừng.
2. Tính mức `L` qua toàn bộ chuỗi. Mức cuối `L(n)` là nhu cầu dự báo mỗi ngày (`daily_level`).
3. Nhu cầu dự báo trong tầm nhìn: `forecast_qty = daily_level × HorizonDays`.
4. Số lượng đề xuất nhập: `suggested_reorder_qty = max(0, làm tròn lên(daily_level × (LeadTimeDays + SafetyDays)) − available)`.

## Backtest

1. Cắt chuỗi: phần huấn luyện là mọi ngày trừ `BacktestDays` ngày cuối; phần kiểm tra là `BacktestDays` ngày cuối.
2. Dự báo: dùng mức cuối của phần huấn luyện làm dự báo cho mọi ngày của phần kiểm tra.
3. Phương án cơ sở: trung bình 7 ngày cuối của phần huấn luyện, dùng cho mọi ngày của phần kiểm tra.
4. Sai số tính gộp trên mọi chi nhánh và SKU đủ dữ liệu:

```math
\mathrm{WAPE} = \frac{\sum |y - \hat{y}|}{\sum y}
```

5. Lưu `forecast_wape` và `baseline_wape` vào `forecast_runs`. Không ngày nào sau mốc cắt được dùng để tạo dự báo cho phần kiểm tra.

Tổng thực tế của phần kiểm tra bằng 0 thì không tính WAPE và ghi rõ là không có dữ liệu để đánh giá.

## Chạy

- Job Hangfire chạy mỗi đêm, lặp qua từng tenant, mỗi tenant một tenant context.
- Owner chạy tay qua `POST /api/insights/forecast/run`.
- Mỗi lần chạy tạo một dòng `forecast_runs`. Lỗi thì ghi trạng thái Failed kèm thông báo; kết quả của lần chạy thành công trước vẫn được giữ và hiển thị.

## Giới hạn đã biết

- Ngày hết hàng được tính là ngày không có nhu cầu, nên SKU hay hết hàng bị dự báo thấp.
- Không xét mùa vụ, ngày lễ hay khuyến mãi.
- Dữ liệu seed chỉ để trình diễn chức năng; WAPE trên dữ liệu seed không nói lên độ chính xác trên dữ liệu thật.

## Điều không được làm

Kết quả dự báo chỉ là khuyến nghị. Code dự báo không tạo phiếu nhập, không ghi ledger và không đổi ngưỡng tồn.
