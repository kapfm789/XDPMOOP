# ADR-0010: Dự báo nhập hàng bằng phương pháp thống kê, chạy trong insights

- Trạng thái: Đề xuất, chờ giảng viên xác nhận
- Gộp từ: D-14
- Ngày: 2026-10-03

## Bối cảnh

Gói công việc 5 của đề ghi "tích hợp dự báo bằng AI để đề xuất nhập hàng bổ sung". Phần yêu cầu chức năng không có mã nào cho việc này; đề không nêu mô hình, dữ liệu đầu vào, đầu ra hay tiêu chí nghiệm thu. Không thể vì thiếu mã mà bỏ nó khỏi phạm vi.

## Quyết định

- Đặt ba mã tạm: FR-AI-01 dự báo nhu cầu, FR-AI-02 hiển thị đề xuất nhập, FR-AI-03 backtest.
- Phương pháp: san bằng hàm mũ đơn trên chuỗi số lượng bán theo ngày, cho từng SKU tại từng chi nhánh. Viết bằng C# trong `insights`, không dùng mô hình ngôn ngữ, không gọi dịch vụ ngoài.
- Chạy mỗi đêm bằng Hangfire; Owner chạy tay được.
- Đầu ra là khuyến nghị số lượng nên nhập. Hệ thống không tự tạo phiếu mua và không ghi ledger.
- SKU có ít hơn 14 ngày lịch sử thì hiện "chưa đủ dữ liệu", không đưa con số.
- Nghiệm thu bằng backtest: giấu 14 ngày cuối, so sai số WAPE của dự báo với phương án cơ sở là trung bình 7 ngày gần nhất.

Thuật toán và tham số ở [design/forecast.md](../design/forecast.md).

## Hệ quả

- Gói demo chạy được mà không cần khóa API hay mạng ra ngoài.
- Dự báo tách khỏi cảnh báo ngưỡng tĩnh của FR-REP-03; cảnh báo ngưỡng không được coi là đã có AI.
- Giới hạn đã biết: ngày hết hàng bị tính là ngày không có nhu cầu, nên dự báo thấp hơn thực tế với SKU hay hết hàng.
- Dữ liệu seed chỉ minh họa chức năng; nó không chứng minh độ chính xác trên dữ liệu kinh doanh thật.

## Phương án đã loại

- Gọi mô hình ngôn ngữ hoặc dịch vụ dự báo ngoài: thêm phụ thuộc, khóa bí mật và chi phí; đề không yêu cầu.
- Mô hình học máy riêng bằng Python: thêm một service và một ngôn ngữ cho 3 dev trong 5 tuần.

## Nếu bị đổi

Giảng viên yêu cầu mô hình học máy hoặc mô hình ngôn ngữ: tách forecast thành service riêng nhận cùng event; giữ nguyên API đề xuất nhập để giao diện không phải đổi.
