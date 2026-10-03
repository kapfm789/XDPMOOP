# tools/k6 — kế hoạch folder

Kịch bản đo tải cho NFR-PERF-01 và NFR-PERF-02. Owner: Dev B. Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Cấu trúc

```text
k6/
├─ pos-search.js      tìm SKU và quét mã, mục tiêu p95 dưới 200 ms
├─ create-order.js    tạo đơn và POS checkout, mục tiêu dưới 500 ms
├─ gross-profit.js    báo cáo doanh thu với 100.000 bản ghi, mục tiêu dưới 2 giây
└─ flash-sale.js      50 đơn đồng thời cho 1 sản phẩm
```

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 9 | W5-03 | Đo tải bằng k6: tìm SKU, tạo đơn, báo cáo, flash sale 50 đồng thời | B | NFR-PERF-01, NFR-PERF-02 | Báo cáo đo ghi rõ cấu hình máy và dữ liệu |

## Quy tắc riêng

- Báo cáo đo ghi rõ cấu hình máy, lượng dữ liệu, số người dùng ảo và thời gian chạy (kịch bản T17).
- Kịch bản flash sale kiểm cả tính đúng lẫn độ trễ: đúng 1 đơn thành công, 49 đơn thất bại có kiểm soát.
- Chạy trên dữ liệu seed của `deploy/seed`, không chạy trên database trống.
