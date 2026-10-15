# Báo cáo backtest dự báo trên dữ liệu seed

Kết quả backtest của dự báo nhập hàng (task W4-05, FR-AI-03, kịch bản T18) trên dữ liệu của [deploy/seed](../../deploy/seed/README.md). Thuật toán, tham số và cách tính WAPE ở [design/forecast.md](../design/forecast.md).

## Cách chạy lại

1. `docker compose -f deploy/docker-compose.yml up -d`
2. `docker compose -f deploy/docker-compose.yml run --rm seed`: seed phát 60 ngày lịch sử bán, chờ `insights` nhận đủ rồi gọi `POST /api/insights/forecast/run` cho từng tenant.
3. Đăng nhập Owner của tenant, mở `/forecast` trên `admin`: dòng "Sai số backtest (WAPE)" là hai số dưới đây. Seed in cùng hai số đó ra màn hình.

## Kết quả lần chạy 2026-10-15

Tham số mặc định: `Alpha` 0,3; 60 ngày lịch sử tính tới hết ngày 2026-10-14; giấu 14 ngày cuối (2026-10-01 tới 2026-10-14) khỏi phần huấn luyện. Phương án cơ sở là trung bình 7 ngày cuối của phần huấn luyện.

| Tenant | SKU × chi nhánh | Đơn vị bán trong 60 ngày | WAPE dự báo | WAPE cơ sở |
| --- | --- | --- | --- | --- |
| Thời trang Mộc | 47 × 2 | 3.771 | 0,9673 | 0,9561 |
| Tạp hóa Xanh | 46 × 2 | 15.544 | 0,4814 | 0,4868 |

Chạy seed lần hai cùng ngày cho đúng hai cặp số trên: lịch sử sinh lại y hệt và `insights` bỏ qua bản trùng.

## Đọc kết quả

- WAPE tính ở mức từng SKU tại từng chi nhánh theo từng ngày. Thời trang Mộc bán trung bình chưa tới 1 cái mỗi SKU mỗi ngày, nên phần lớn sai số là nhiễu ngẫu nhiên của từng ngày mà không phương pháp nào bắt được; cả hai WAPE đều gần 1. Tạp hóa Xanh bán đều và nhiều hơn nên WAPE thấp hơn hẳn.
- Trên hai bộ dữ liệu này, san bằng hàm mũ và trung bình 7 ngày cho sai số gần bằng nhau: dữ liệu seed có xu hướng nhẹ và không có mùa vụ, đúng kiểu mà trung bình trượt đã làm tốt.
- Dữ liệu seed là dữ liệu giả lập để trình diễn chức năng (ADR-0010). Hai con số trên chứng minh backtest chạy đúng quy trình (giấu 14 ngày cuối, so với phương án cơ sở), không nói lên độ chính xác trên dữ liệu bán hàng thật.
