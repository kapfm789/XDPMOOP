# backend/services/insights — kế hoạch folder

Báo cáo, cảnh báo tồn, dự báo nhập hàng và thông báo realtime, dựng từ event của `core` (FR-REP-01..03, FR-AI-01..03, FR-SIM-02, phần tổng hợp ngày của FR-SIM-03). Owner: Dev A (báo cáo, cảnh báo), Dev B (AI, SignalR). Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Phân lớp

```text
insights/
├─ src/
│  ├─ Oism.Insights.Domain/           SalesFact, StockSnapshot, DailyAggregate, ForecastRun, ForecastResult, ExponentialSmoothing
│  ├─ Oism.Insights.Application/      projection từ event, GrossProfitReport, InventoryValue, TopSellers, LowStockAlerts, RunForecast
│  ├─ Oism.Insights.Infrastructure/   InsightsDbContext, migration, inbox, Hangfire (tổng hợp ngày, dự báo đêm)
│  └─ Oism.Insights.Api/              ReportsController, AlertsController, ForecastController, NotificationsHub, consumer
└─ tests/
   ├─ Oism.Insights.UnitTests/           công thức doanh thu, giá vốn, lợi nhuận gộp, dự báo
   └─ Oism.Insights.IntegrationTests/    phát lại event, cách ly tenant trên SignalR
```

Phụ thuộc chỉ hướng vào trong: Api → Application → Domain; Infrastructure cài đặt interface của Application.

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 7 | W4-01 | insights: consumer `OrderConfirmed`, `OrderCancelled`, `StockChanged` vào SalesFact, StockSnapshot | A | FR-REP-01, FR-REP-02 | Phát lại event không cộng trùng |
| 7 | W4-02 | insights: báo cáo doanh thu, giá vốn, lợi nhuận gộp; lọc bốn chiều; tổng hợp ngày bằng Hangfire | A | FR-REP-01, FR-SIM-03, NFR-PERF-01 | Ví dụ 300.000 − 220.000 = 80.000 khớp; dưới 2 giây với 100.000 bản ghi |
| 7 | W4-04 | insights: SignalR hub, group theo tenant và chi nhánh, đẩy đơn mới và cảnh báo tồn | B | FR-SIM-02 | Phiên của tenant khác không nhận thông báo |
| 8 | W4-03 | insights: giá trị tồn theo giá vốn, bán chạy và bán chậm 30 ngày, cảnh báo available ≤ Threshold | A | FR-REP-02, FR-REP-03 | Hạ tồn dưới ngưỡng thì cảnh báo xuất hiện |
| 8 | W4-05 | insights: dự báo exponential smoothing, job đêm, API đề xuất nhập, backtest WAPE | B | FR-AI-01..03 | Có báo cáo backtest trên seed; SKU thiếu lịch sử hiện "chưa đủ dữ liệu" |
| 9 | W5-02 | Bộ test cách ly tenant toàn hệ thống: API, event, SignalR, job | A | NFR-TENANT-01 | Mọi ca dùng ID tenant khác nhận 404 hoặc 403 |

Skeleton của folder được tạo từ mẫu ở phase 1 (W1-02); nghiệp vụ bắt đầu ở phase 7.

## Sở hữu

- Schema `insights` của database `oism`: SalesFact, StockSnapshot, DailyAggregate, ForecastRun, ForecastResult (mục 5 của kế hoạch tổng).
- API: `/reports/gross-profit`, `/reports/inventory-value`, `/reports/top-sellers`, `/reports/slow-movers`, `/alerts/low-stock`, `/forecast/reorder-suggestions`, `/forecast/run`, WebSocket `/hubs/notifications` (mục 8).
- Event nhận: `SkuUpserted`, `OrderReserved`, `OrderRejected`, `OrderConfirmed`, `OrderCancelled`, `StockChanged` (mục 7). Không phát event.
- Kiểm thử: T10, T15, T18, T19, T22.

## Quy tắc riêng

- Chỉ đọc dữ liệu từ event, không truy cập schema `core`.
- Giá vốn trong báo cáo lấy từ `costPrice` đã chốt trong `OrderConfirmed`, không lấy giá vốn hiện tại (FR-COST-02).
- Công thức báo cáo theo D-12; báo cáo lợi nhuận chỉ Owner xem (D-15).
- Dự báo chỉ là khuyến nghị: không tự tạo phiếu mua, không ghi ledger (D-14).
- SignalR group theo tenant và chi nhánh; không gửi vào group chung.
