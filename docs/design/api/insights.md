# API: insights

Tiền tố `/api/insights`, cùng hub SignalR `/hubs/notifications`. Use case ở [reports.md](../../usecase-userstory/reports.md) và [channel-realtime.md](../../usecase-userstory/channel-realtime.md); bảng ở [data-model/insights.md](../data-model/insights.md); công thức ở [ADR-0008](../../decisions/0008-report-formulas.md). Quy ước chung ở [README.md](README.md).

## Báo cáo

| Phương thức | Đường dẫn | Vai trò | Vào | Ra |
| --- | --- | --- | --- | --- |
| GET | `/api/insights/reports/gross-profit` | Owner | `from`, `to`, `branchId?`, `channel?`, `skuId?`, `groupBy?` | `totals` và `rows[]`, mỗi dòng có `revenue`, `cogs`, `grossProfit`, `quantity` |
| GET | `/api/insights/reports/inventory-value` | Owner, Staff | `branchId?`, `page`, `pageSize` | Dòng theo chi nhánh và SKU: `onHand`, `avgCost`, `value`; kèm `totalValue` |
| GET | `/api/insights/reports/top-sellers` | Owner, Staff | `days?` (mặc định 30), `branchId?`, `limit?` (mặc định 20) | SKU xếp giảm dần theo `quantity` |
| GET | `/api/insights/reports/slow-movers` | Owner, Staff | `days?` (mặc định 30), `branchId?`, `limit?` (mặc định 20) | SKU còn tồn, xếp tăng dần theo `quantity` |

`groupBy` nhận `day`, `branch`, `channel` hoặc `sku`; bỏ trống thì chỉ trả `totals`. `from` và `to` là ngày theo giờ Việt Nam, tính cả hai đầu. Với Staff, `/reports/inventory-value` không trả `avgCost` và `value`.

## Cảnh báo và dự báo

| Phương thức | Đường dẫn | Vai trò | Vào | Ra |
| --- | --- | --- | --- | --- |
| GET | `/api/insights/alerts/low-stock` | Owner, Staff | `branchId?` | SKU có `available <= threshold`: `branchId`, `skuId`, `skuCode`, `name`, `available`, `threshold` |
| GET | `/api/insights/forecast/reorder-suggestions` | Owner | `branchId?`, `page`, `pageSize` | `run` (thời điểm, trạng thái, hai chỉ số WAPE) và `items[]` |
| POST | `/api/insights/forecast/run` | Owner | | 202: `runId` |

Mỗi phần tử của `items[]`: `branchId`, `skuId`, `skuCode`, `name`, `hasEnoughData`, `forecastQty?`, `available`, `suggestedReorderQty?`. Khi `hasEnoughData` là false thì hai trường số để trống. Thuật toán ở [forecast.md](../forecast.md).

## Thông báo realtime

Kết nối WebSocket tới `/hubs/notifications` kèm access token. Server tự đưa kết nối vào group theo token:

| Người dùng | Group |
| --- | --- |
| Owner, Staff | `tenant:{tenantId}` |
| Người dùng có `branch_id` trong token | `tenant:{tenantId}:branch:{branchId}` |

Client không tự chọn group. Tên và trường của thông báo `OrderCreated`, `LowStock` ở [events.md](../events.md).

## Đường vào không phải HTTP

| Nguồn | Tên | Xử lý |
| --- | --- | --- |
| RabbitMQ | `OrderConfirmed` | Thêm một dòng `sales_facts` cho mỗi dòng đơn; khóa chính chặn cộng đôi |
| RabbitMQ | `StockChanged` | Ghi đè `stock_snapshots` theo `version`; phát `LowStock` khi vừa chạm ngưỡng |
| RabbitMQ | `OrderReserved` | Phát `OrderCreated` qua SignalR |
| RabbitMQ | `OrderRejected`, `OrderCancelled` | Ghi nhận để thống kê; không tác động số liệu doanh thu |
| RabbitMQ | `SkuUpserted` | Ghi đè `sku_names` theo `version` |
| Hangfire, mỗi đêm | Tổng hợp ngày | Dựng lại `daily_aggregates` cho ngày hôm qua, từng tenant |
| Hangfire, mỗi đêm | Dự báo | Chạy dự báo cho từng tenant |
