# Báo cáo đo tải

Kết quả đo của task W5-03 cho NFR-PERF-01 (kịch bản T17) và NFR-PERF-02 trên gói Compose (kịch bản T01). Kịch bản, tải và cách chạy: [tools/k6/README.md](../../tools/k6/README.md).

## Cấu hình đo

| Mục | Giá trị |
| --- | --- |
| Ngày đo | 2026-10-18 |
| Máy | Laptop Intel Core i5-10200H 2,4 GHz, 4 nhân 8 luồng, RAM 7,8 GB, Windows 10 Home 22H2 |
| Docker | Docker Desktop, engine 29.8.2, máy ảo WSL 2 được cấp 8 luồng và khoảng 3,7 GB RAM |
| Hệ thống | Gói demo `deploy/docker-compose.demo.yml` ở nhánh phase 9: image Release của gateway và 5 service, PostgreSQL 17, RabbitMQ 4 |
| Công cụ đo | k6 1.0.0, chạy trong cùng mạng Compose và cùng máy ảo với hệ thống, gọi thẳng gateway qua HTTP |
| Dữ liệu | Seed của `deploy/seed`: 2 tenant, mỗi tenant 2 chi nhánh, 47 và 46 SKU. Tenant đo là Tạp hóa Xanh: 111.381 dòng `sales_facts` trong 60 ngày (khoảng 11.400 dòng của seed và 100.000 dòng của `prepare-sales.sql`), không có dòng `daily_aggregates` nào nên báo cáo đọc thẳng `sales_facts` |

k6, PostgreSQL, RabbitMQ và 6 tiến trình .NET dùng chung 8 luồng của một laptop, nên số đo dưới đây là cận trên: trên máy chủ riêng, độ trễ sẽ thấp hơn.

## Kết quả

| Kịch bản | Tải | Số request | p50 | p95 | Ngưỡng | Kết quả |
| --- | --- | --- | --- | --- | --- | --- |
| Tìm SKU ở POS | 20 người dùng ảo, không nghỉ, 1 phút | 52.918 (876/giây) | 19,5 ms | 43,9 ms | dưới 200 ms | Đạt |
| POS checkout | 10 quầy, nghỉ 1 giây giữa hai đơn, 1 phút | Cùng với dòng dưới: 859 đơn (14/giây) | 45,2 ms | 141,1 ms | dưới 500 ms | Đạt |
| Tạo đơn thủ công | 5 người, nghỉ 1 giây giữa hai đơn, 1 phút | | 26,7 ms | 121,3 ms | dưới 500 ms | Đạt |
| Báo cáo doanh thu 60 ngày, 111.381 dòng | 5 người dùng ảo, không nghỉ, 1 phút | 1.867 (31/giây) | 136,0 ms | 292,6 ms | dưới 2 giây | Đạt |
| Flash sale: 50 đơn cùng lúc cho 1 cái cuối | 50 người dùng ảo, mỗi người 1 đơn | 50 | 382,5 ms | 412,4 ms | Đúng 1 thành công | Đạt: 1 đơn 201, 49 đơn 409 `insufficient_stock`, 0 lỗi khác; sau cùng tồn 1, giữ 1, khả dụng 0 |

Không request nào lỗi ở mọi kịch bản.

## Mức bão hòa của ghi đơn

Chạy `create-order.js` với `K6_THINK=0`: 15 người dùng ảo gửi đơn liên tục không nghỉ, mọi đơn chọn 2 trong 6 SKU của cùng một chi nhánh.

| Kịch bản | Số request | p50 | p95 | So với ngưỡng 500 ms |
| --- | --- | --- | --- | --- |
| POS checkout | Tổng 2.537 (41/giây) | 257 ms | 876 ms | Vượt |
| Tạo đơn thủ công | | 228 ms | 805 ms | Vượt |

Không request nào lỗi. Độ trễ tăng vì hai lẽ. Mọi đơn tranh nhau đúng 6 dòng số dư, mà mỗi đơn khóa các dòng đó bằng `SELECT ... FOR UPDATE` tới hết transaction ([ADR-0003](../decisions/0003-ledger-balance-and-locking.md)), nên các đơn xếp hàng lần lượt. Thêm vào đó, trên cùng 8 luồng, `insights` dùng khoảng 80% một luồng để nhận event của từng đơn, PostgreSQL 72% và `core` 62%. 41 đơn mỗi giây trên 6 SKU của một cửa hàng cao hơn nhiều so với tải bán lẻ thực tế; ở tải 14 đơn mỗi giây có nghỉ như bảng trên, p95 dưới 150 ms.
