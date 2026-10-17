# Đo tải bằng k6

Kịch bản đo cho NFR-PERF-01 và NFR-PERF-02 (task W5-03, kịch bản T17 và T01 trên gói Compose). Kế hoạch: [docs/plans/tools-k6.md](../../docs/plans/tools-k6.md). Kết quả đo: [docs/testing/load-test.md](../../docs/testing/load-test.md).

| File | Đo gì | Tải | Ngưỡng |
| --- | --- | --- | --- |
| `pos-search.js` | Thu ngân tìm SKU ở POS theo tên, mã | 20 người dùng ảo, không nghỉ, 1 phút | p95 dưới 200 ms |
| `create-order.js` | POS checkout và tạo đơn thủ công | 10 quầy POS và 5 người tạo đơn, nghỉ 1 giây giữa hai đơn, 1 phút | p95 dưới 500 ms |
| `gross-profit.js` | Báo cáo doanh thu 60 ngày, không nhóm và nhóm theo ngày, chi nhánh, kênh, SKU | 5 người dùng ảo, không nghỉ, 1 phút | p95 dưới 2 giây |
| `flash-sale.js` | 50 đơn cùng lúc cho 1 SKU còn đúng 1 cái | 50 người dùng ảo, mỗi người 1 đơn | Đúng 1 đơn 201, 49 đơn 409 `insufficient_stock`, sau cùng tồn 1, giữ 1, khả dụng 0 |
| `prepare-sales.sql` | Thêm 100.000 dòng bán cho tenant Tạp hóa Xanh trước khi đo báo cáo | | |
| `lib.js` | Đăng nhập, gọi gateway, tra chi nhánh và SKU của seed | | |

Mọi kịch bản đăng nhập bằng tài khoản seed của Tạp hóa Xanh ([deploy/seed/README.md](../../deploy/seed/README.md)) và chỉ đi qua gateway. Bước chuẩn bị đổi dữ liệu bằng chính API của hệ thống: `create-order.js` nhập thêm một lô 20.000 cho mỗi SKU nó bán, `flash-sale.js` dùng phiên kiểm kê để đưa tồn của SKU `HD-RM` ở chi nhánh `XANH-DD` về đúng 1 và hủy đơn thắng ở cuối. Riêng `prepare-sales.sql` ghi thẳng vào `insights.sales_facts`, vì 100.000 dòng bán qua đường event sẽ mất quá lâu cho một lần đo.

## Chạy

Trên gói demo đã nạp seed ([deploy/README.md](../../deploy/README.md)), từ gốc repo:

```bash
docker compose -f deploy/docker-compose.demo.yml exec -T postgres psql -U oism -d oism < tools/k6/prepare-sales.sql
docker compose -f deploy/docker-compose.demo.yml run --rm k6 run pos-search.js
docker compose -f deploy/docker-compose.demo.yml run --rm k6 run create-order.js
docker compose -f deploy/docker-compose.demo.yml run --rm k6 run gross-profit.js
docker compose -f deploy/docker-compose.demo.yml run --rm k6 run flash-sale.js
```

k6 chạy trong mạng của gói demo và gọi `http://gateway:8080`. Ngưỡng nào vượt thì k6 in dấu ✗ và thoát mã khác 0.

`create-order.js` nhận `K6_THINK` là số giây nghỉ giữa hai đơn, mặc định 1. Đặt 0 để đo mức bão hòa:

```bash
docker compose -f deploy/docker-compose.demo.yml run --rm -e K6_THINK=0 k6 run create-order.js
```

Đơn thủ công do kịch bản tạo nằm ở Reserved và tự hết hạn sau 30 phút như đơn thật; trước đó chúng hiện trong danh sách đơn chờ duyệt của Tạp hóa Xanh.
