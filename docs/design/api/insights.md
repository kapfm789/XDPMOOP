# API: insights

Tiền tố `/api/insights`, cùng hub SignalR `/hubs/notifications`. Use case ở [reports.md](../../usecase-userstory/reports.md) và [channel-realtime.md](../../usecase-userstory/channel-realtime.md); bảng ở [data-model/insights.md](../data-model/insights.md); công thức ở [ADR-0008](../../decisions/0008-report-formulas.md). Quy ước chung ở [README.md](README.md).

## Báo cáo

| Phương thức | Đường dẫn | Vai trò | Vào | Ra |
| --- | --- | --- | --- | --- |
| GET | `/api/insights/reports/gross-profit` | Owner | `from`, `to`, `branchId?`, `channel?`, `skuId?`, `groupBy?` | `totals` và `rows[]`, mỗi dòng có `revenue`, `cogs`, `grossProfit`, `quantity` |
| GET | `/api/insights/reports/inventory-value` | Owner, Staff | `branchId?`, `page`, `pageSize` | Khuôn phân trang; mỗi dòng theo chi nhánh và SKU: `branchId`, `skuId`, `skuCode`, `name`, `onHand`, `avgCost`, `value`; kèm `totalValue` |
| GET | `/api/insights/reports/top-sellers` | Owner, Staff | `days?` (mặc định 30), `branchId?`, `limit?` (mặc định 20) | Mảng SKU: `skuId`, `skuCode`, `name`, `quantity`, xếp giảm dần theo `quantity` |
| GET | `/api/insights/reports/slow-movers` | Owner, Staff | `days?` (mặc định 30), `branchId?`, `limit?` (mặc định 20) | Mảng SKU còn tồn: `skuId`, `skuCode`, `name`, `quantity`, `onHand`, xếp tăng dần theo `quantity` |

`groupBy` nhận `day`, `branch`, `channel` hoặc `sku`; bỏ trống thì chỉ trả `totals` và `rows` rỗng. `from` và `to` bắt buộc, dạng `yyyy-MM-dd`, là ngày theo giờ Việt Nam, tính cả hai đầu; thiếu, sai dạng, `from` sau `to` hay `groupBy` lạ trả 400 `validation_failed`.

`/reports/inventory-value` đọc `stock_snapshots`, chỉ gồm dòng còn tồn (`onHand > 0`); `value` là `onHand × avgCost` và `totalValue` là tổng `value` của mọi dòng khớp bộ lọc, không chỉ trang đang xem. Owner nhận dòng xếp theo `value` giảm dần. Với Staff, `avgCost`, `value` và `totalValue` để trống, và dòng xếp theo `onHand` giảm dần để thứ tự không lộ giá vốn.

`days` từ 1 tới 365 là số ngày gần nhất, tính cả hôm nay theo giờ Việt Nam; `limit` từ 1 tới 100; ngoài khoảng trả 400 `validation_failed`, cùng như `page` nhỏ hơn 1 hay `pageSize` ngoài khoảng 1 tới 100. `quantity` là tổng số lượng của `sales_facts` trong cửa sổ, theo SKU, cộng mọi chi nhánh khi không lọc chi nhánh. Bán chậm chỉ gồm SKU có tổng `onHand` lớn hơn 0 trong phạm vi lọc; SKU không bán được gì có `quantity` bằng 0 và đứng đầu; cùng `quantity` thì SKU tồn nhiều hơn đứng trước. `skuCode` và `name` lấy từ `sku_names`, để trống nếu chưa nhận `SkuUpserted`.

Mỗi dòng của `/reports/gross-profit` có `key` là giá trị của chiều đang nhóm: ngày `yyyy-MM-dd`, `branchId`, tên kênh hoặc `skuId`; khi nhóm theo SKU thì có thêm `skuCode` và `name` lấy từ `sku_names` (để trống nếu chưa nhận `SkuUpserted`). Nhóm theo ngày xếp theo ngày tăng dần; các nhóm khác xếp theo `revenue` giảm dần. `totals` gồm `quantity`, `revenue`, `cogs`, `grossProfit`.

## Cảnh báo và dự báo

| Phương thức | Đường dẫn | Vai trò | Vào | Ra |
| --- | --- | --- | --- | --- |
| GET | `/api/insights/alerts/low-stock` | Owner, Staff | `branchId?` | Mảng SKU có `available <= threshold`: `branchId`, `skuId`, `skuCode`, `name`, `available`, `threshold`, xếp `available` tăng dần |
| GET | `/api/insights/forecast/reorder-suggestions` | Owner | `branchId?`, `page`, `pageSize` | `run` (thời điểm, trạng thái, hai chỉ số WAPE), `lastSucceededRun` và `items[]` theo khuôn phân trang |
| POST | `/api/insights/forecast/run` | Owner | | 202: `runId` |

`run` là lần chạy gần nhất, có thể là lần lỗi; `lastSucceededRun` là lần chạy thành công gần nhất, trùng `run` khi lần gần nhất thành công, và để trống khi chưa có lần nào thành công. Cả hai có `id`, `ranAt`, `method`, `status` (`Succeeded`, `Failed`), `error?`, `forecastWape?`, `baselineWape?`; hai WAPE để trống khi lần chạy lỗi hoặc không có dữ liệu để đánh giá. Chưa có lần chạy nào thì `run` để trống.

`items[]` lấy từ `lastSucceededRun`, kèm `page`, `pageSize`, `total` như khuôn phân trang chung. Mỗi phần tử: `branchId`, `skuId`, `skuCode`, `name`, `hasEnoughData`, `forecastQty?`, `available`, `suggestedReorderQty?`. Khi `hasEnoughData` là false thì hai trường số để trống. `available` là tồn khả dụng hiện tại trong `stock_snapshots`, 0 khi chưa có dòng nào. Dòng đủ dữ liệu đứng trước, xếp `suggestedReorderQty` rồi `forecastQty` giảm dần. Thuật toán ở [forecast.md](../forecast.md).

`POST /forecast/run` chạy dự báo cho tenant của người gọi ngay trong request và trả 202 khi lần chạy đã được ghi, kể cả khi lần chạy đó lỗi; kết quả và trạng thái xem ở `reorder-suggestions`.

## Thông báo realtime

Kết nối WebSocket tới `/hubs/notifications` kèm access token. Server tự đưa kết nối vào group theo token:

| Người dùng | Group |
| --- | --- |
| Owner, Staff | `tenant:{tenantId}`, kể cả khi token có `branch_id` |
| Người dùng khác có `branch_id` trong token (Cashier) | `tenant:{tenantId}:branch:{branchId}` |

Mỗi kết nối vào đúng một group, nên `OrderCreated` gửi cho cả hai group không tới ai hai lần. Cashier không có `branch_id` thì không vào group nào. Client không tự chọn group và không gọi method nào của hub. Thông báo chỉ được gửi sau khi transaction của consumer đã commit. Tên và trường của thông báo `OrderCreated`, `LowStock` ở [events.md](../events.md).

## Đường vào không phải HTTP

| Nguồn | Tên | Xử lý |
| --- | --- | --- |
| RabbitMQ | `OrderConfirmed` | Thêm một dòng `sales_facts` cho mỗi dòng đơn chưa có; khóa chính chặn cộng đôi; xóa `daily_aggregates` của ngày đó nếu đã tổng hợp |
| RabbitMQ | `StockChanged` | Ghi đè `stock_snapshots` theo `version`; phát `LowStock` khi vừa chạm ngưỡng |
| RabbitMQ | `OrderReserved` | Phát `OrderCreated` qua SignalR |
| RabbitMQ | `OrderRejected`, `OrderCancelled` | Chỉ ghi `inbox_messages`; không tác động số liệu doanh thu |
| RabbitMQ | `SkuUpserted` | Ghi đè `sku_names` theo `version` |
| Hangfire, mỗi đêm | Tổng hợp ngày | Dựng lại `daily_aggregates` cho ngày hôm qua, từng tenant |
| Hangfire, mỗi đêm (00:30 giờ Việt Nam) | Dự báo | Chạy dự báo cho từng tenant có `sales_facts` hoặc `stock_snapshots` |
