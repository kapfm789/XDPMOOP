# API: core

Tiền tố `/api/core`. Use case ở [inventory.md](../../usecase-userstory/inventory.md), [orders.md](../../usecase-userstory/orders.md), [pos.md](../../usecase-userstory/pos.md); bảng ở [data-model/core.md](../data-model/core.md); trình tự ở [flows/](../flows/). Quy ước chung ở [README.md](README.md).

## POS

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/core/pos/skus` | Cashier, Staff, Owner | `query`, `branchId` | Tối đa 20 SKU: `skuId`, `skuCode`, `name`, `barcodes`, `retailPrice`, `available` | |
| POST | `/api/core/pos/checkout` | Cashier, Owner | Header `Idempotency-Key`; `branchId`, `items[]`, `payment` | 201: đơn ở Completed kèm dòng đơn và thanh toán | 409 `insufficient_stock`; 403 khi Cashier sai chi nhánh |

`items[]`: `skuId`, `quantity`, `unitPrice?`, `discount?`. Bỏ trống `unitPrice` thì dùng giá lẻ hiện tại. `payment`: `method` (`Cash` hoặc `QR`), `amount`. Gửi lại cùng `Idempotency-Key` trả 200 với đơn đã tạo.

- `/pos/skus` chỉ trả SKU đang bán. `query` khớp một phần tên, mã SKU hoặc mã vạch, không phân biệt hoa thường; bỏ trống thì trả các SKU đầu tiên theo `skuCode`. SKU khớp đúng mã SKU hoặc đúng một mã vạch đứng trước, rồi tới các SKU còn lại theo `skuCode`, để mã vừa quét luôn nằm trong 20 dòng trả về. `available` là `onHand - reserved` tại `branchId`; SKU chưa có dòng số dư ở chi nhánh, hoặc đã hết hàng, vẫn hiện với `available = 0`. Thiếu `branchId` trả 400 `validation_failed`.
- Cashier chỉ dùng được hai endpoint này cho chi nhánh trong claim `branch_id` của token: `branchId` khác đi, hoặc token không có claim, trả 403 `forbidden`. Owner và Staff dùng được với mọi chi nhánh của tenant.
- `/pos/checkout` trả đơn theo khuôn ở mục "Đơn hàng", ở `Completed`, `confirmedAt` bằng `completedAt`, kèm `payment` gồm `method`, `amount`, `confirmedBy`, `confirmedAt`. `payment.amount` là số tiền khách đưa và được lưu nguyên như vậy; nhỏ hơn `totalAmount` thì trả 400.
- `/pos/checkout` trả 400 `validation_failed` khi thiếu header `Idempotency-Key` hoặc header không phải uuid, `items[]` rỗng, `quantity` không dương, `unitPrice` hoặc `discount` âm, `discount` vượt thành tiền của dòng, thiếu `payment`, `method` khác `Cash` và `QR`. `branchId` hoặc `skuId` chưa có bản sao ở `core` trả 409 `reference_not_ready`; chi nhánh đã tắt hoặc SKU ngừng bán trả 409 `inactive_reference`.
- Khóa quyết định, không phải nội dung: gửi lại cùng `Idempotency-Key` với giỏ khác vẫn nhận lại đúng đơn đã tạo. Lần gửi bị từ chối (4xx) hoặc lỗi không để lại gì, nên khóa đó dùng lại được.

## Tồn và sổ

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/core/stock` | Owner, Staff | `branchId?`, `skuId?`, `query?`, `page`, `pageSize` | Dòng số dư: `branchId`, `skuId`, `skuCode`, `name`, `onHand`, `reserved`, `available`, `avgCost`, `threshold?` | |
| PUT | `/api/core/stock/threshold` | Owner, Staff | `branchId`, `skuId`, `threshold?` | 200: dòng số dư | 400 khi âm; 409 `reference_not_ready` |
| GET | `/api/core/ledger` | Owner, Staff | `branchId?`, `skuId?`, `from?`, `to?`, `page`, `pageSize` | Dòng sổ theo thứ tự ghi | |

Staff không thấy `avgCost` trong phản hồi của `/stock` và `unitCost` trong `/ledger`; hai trường này chỉ trả cho Owner.

- `/stock` trả theo khuôn phân trang chung, xếp theo `skuCode` rồi `branchId`. `query` khớp một phần mã hoặc tên SKU, không phân biệt hoa thường. `available` bằng `onHand - reserved`.
- `/ledger` trả theo khuôn phân trang chung, xếp theo `seq` tăng dần. Mỗi dòng gồm `seq`, `id`, `branchId`, `skuId`, `skuCode`, `name`, `type` (`IN`, `OUT`), `reason`, `quantity`, `balanceAfter`, `unitCost` (chỉ Owner), `referenceType`, `referenceId`, `reversalOfId?`, `createdBy?`, `createdAt`. `from` và `to` lọc theo `createdAt`, tính cả hai đầu.
- `page` nhỏ hơn 1 hoặc `pageSize` ngoài khoảng 1 đến 100 trả 400 `validation_failed`.
- `/stock/threshold` trả dòng số dư theo đúng khuôn một dòng của `/stock`. `threshold` bỏ trống hoặc `null` là bỏ ngưỡng, SKU đó không còn sinh cảnh báo; số âm trả 400 `validation_failed`. `branchId` hoặc `skuId` chưa có bản sao ở `core` trả 409 `reference_not_ready`. SKU chưa có dòng số dư tại chi nhánh thì dòng được tạo với tồn 0. Mỗi lần đặt làm `version` của dòng số dư tăng 1 và phát `StockChanged` mang ngưỡng mới.
- Sổ chỉ có đường đọc: không endpoint nào sửa hay xóa dòng sổ (NFR-SEC-03).

## Nhập hàng

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| GET, POST | `/api/core/suppliers` | Owner, Staff | `name`, `phone?` | Nhà cung cấp | |
| GET | `/api/core/purchase-receipts` | Owner, Staff | `branchId?`, `status?`, `page`, `pageSize` | Danh sách phiếu | |
| POST | `/api/core/purchase-receipts` | Owner, Staff | `branchId`, `supplierId`, `note?`, `items[]` | 201: phiếu Draft | 409 `reference_not_ready`, `inactive_reference` |
| PUT | `/api/core/purchase-receipts/{id}` | Owner, Staff | `supplierId`, `note?`, `items[]` | 200: phiếu | 409 `invalid_state_transition` khi không còn Draft |
| POST | `/api/core/purchase-receipts/{id}/confirm` | Owner, Staff | | 200: phiếu Confirmed | |

`items[]`: `skuId`, `quantity`, `unitCost`. Xác nhận lại phiếu đã Confirmed trả 200 với cùng phiếu, không tác động lần hai.

- Nhà cung cấp trả về gồm `id`, `name`, `phone?`, `isActive`; danh sách xếp theo `name`. `name` bị cắt khoảng trắng hai đầu, không được rỗng, tối đa 200 ký tự; `phone` tối đa 20 ký tự; sai thì 400 `validation_failed`.
- Phiếu trả về gồm `id`, `receiptNumber`, `branchId`, `supplierId`, `status` (`Draft`, `Confirmed`), `note?`, `confirmedAt?`, `confirmedBy?`, `createdAt` và `items[]` xếp theo `skuCode`; mỗi dòng gồm `id`, `skuId`, `skuCode`, `skuName`, `quantity`, `unitCost`. Đơn giá nhập trên phiếu trả cho cả Owner lẫn Staff, vì Staff là người nhập nó.
- Danh sách phiếu trả theo khuôn phân trang chung, phiếu mới nhất trước, mỗi phiếu kèm các dòng của nó. `status` nhận `Draft` hoặc `Confirmed`; giá trị khác, `page` nhỏ hơn 1 hoặc `pageSize` ngoài khoảng 1 đến 100 trả 400 `validation_failed`.
- Tạo và sửa phiếu: `items[]` rỗng, `quantity` không dương, `unitCost` âm đều trả 400 `validation_failed`. `supplierId` hoặc `id` phiếu không có trong tenant trả 404 `not_found`. `branchId` hoặc `skuId` chưa có bản sao ở `core` trả 409 `reference_not_ready`; chi nhánh đã tắt trả 409 `inactive_reference`.
- Sửa phiếu thay toàn bộ nhà cung cấp, ghi chú và các dòng; chi nhánh của phiếu không đổi. Tồn chỉ bị tác động khi xác nhận.

## Chuyển kho và kiểm kê

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/core/transfers` | Owner, Staff | `fromBranchId`, `toBranchId`, `items[]` (`skuId`, `quantity`) | 201: phiếu Draft | 400 khi hai chi nhánh trùng nhau |
| POST | `/api/core/transfers/{id}/ship` | Owner, Staff | | 200: phiếu InTransit | 409 `insufficient_stock` |
| POST | `/api/core/transfers/{id}/receive` | Owner, Staff | | 200: phiếu Received | 409 `invalid_state_transition` |
| GET | `/api/core/transfers` | Owner, Staff | `status?`, `page`, `pageSize` | Danh sách phiếu | |
| DELETE | `/api/core/transfers/{id}` | Owner, Staff | | 204 | 409 `invalid_state_transition` khi không còn Draft |
| POST | `/api/core/stocktakes` | Owner, Staff | `branchId` | 201: phiên Open | |
| PUT | `/api/core/stocktakes/{id}/counts` | Owner, Staff | `items[]` (`skuId`, `countedQty`) | 200: phiên | 409 khi đã Posted |
| POST | `/api/core/stocktakes/{id}/post` | Owner, Staff | | 200: phiên Posted kèm chênh lệch từng SKU | 409 `stocktake_below_reserved` |

- Phiếu chuyển kho trả về gồm `id`, `transferNumber`, `fromBranchId`, `toBranchId`, `status` (`Draft`, `InTransit`, `Received`), `shippedAt?`, `receivedAt?`, `createdBy?` và `items[]` xếp theo `skuCode`; mỗi dòng gồm `id`, `skuId`, `skuCode`, `skuName`, `quantity`, `unitCost?`. `unitCost` là giá vốn nơi gửi lúc xuất: chỉ có sau khi xuất và chỉ trả cho Owner.
- Tạo phiếu: hai chi nhánh trùng nhau, `items[]` rỗng, `quantity` không dương đều trả 400 `validation_failed`. Chi nhánh hoặc `skuId` chưa có bản sao ở `core` trả 409 `reference_not_ready`; chi nhánh đã tắt trả 409 `inactive_reference`. Tồn chỉ bị tác động khi xuất.
- Xuất kiểm tồn khả dụng của nơi gửi, không kiểm `onHand`: hàng đang giữ cho đơn không chuyển đi được. Thiếu thì 409 `insufficient_stock` và không dòng nào được xuất. Xuất phiếu không còn Draft trả 409 `invalid_state_transition`.
- Nhận phiếu còn Draft trả 409 `invalid_state_transition`. Nhận lại phiếu đã Received trả 200 với cùng phiếu, không tác động lần hai. `id` phiếu không có trong tenant trả 404 `not_found`.
- Danh sách phiếu trả theo khuôn phân trang chung, mỗi phiếu kèm các dòng của nó: phiếu chưa xuất đứng trước, rồi tới phiếu xuất gần nhất. `status` nhận `Draft`, `InTransit` hoặc `Received`; giá trị khác, `page` nhỏ hơn 1 hoặc `pageSize` ngoài khoảng 1 đến 100 trả 400 `validation_failed`.
- Chỉ phiếu còn Draft xóa được ([ADR-0009](../../decisions/0009-transfer-and-stocktake.md)): phiếu và các dòng của nó biến mất, tồn không bị tác động. Phiếu InTransit hoặc Received trả 409 `invalid_state_transition`.

## Đơn hàng

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/core/orders` | Owner, Staff | `status?`, `channel?`, `branchId?`, `from?`, `to?`, `page`, `pageSize` | Danh sách đơn | |
| GET | `/api/core/orders/{id}` | Owner, Staff | | Đơn kèm dòng đơn, phần giữ hàng, thanh toán | |
| POST | `/api/core/orders` | Owner, Staff | `branchId`, `note?`, `items[]` (`skuId`, `quantity`, `unitPrice?`, `discount?`) | 201: đơn Reserved, kênh Admin | 409 `insufficient_stock`, `reference_not_ready`, `inactive_reference` |
| POST | `/api/core/orders/{id}/confirm` | Owner, Staff | | 200: đơn Confirmed | 409 `invalid_state_transition` |
| POST | `/api/core/orders/{id}/complete` | Owner, Staff | | 200: đơn Completed | 409 `invalid_state_transition` |
| POST | `/api/core/orders/{id}/cancel` | Owner, Staff | | 200: đơn Cancelled | 409 `invalid_state_transition` |

Hủy lại đơn đã Cancelled trả 200 với cùng đơn, không tác động lần hai. `costPrice` trên dòng đơn chỉ trả cho Owner.

- Đơn trả về gồm `id`, `orderNumber`, `branchId`, `channel`, `externalOrderId?`, `status`, `totalAmount`, `reservedUntil?`, `confirmedAt?`, `completedAt?`, `cancelledAt?`, `cancelReason?`, `note?`, `createdBy?`, `createdAt`, `items[]` và `payment?` (chỉ đơn POS); mỗi dòng gồm `id`, `skuId`, `skuCode`, `skuName`, `quantity`, `unitPrice`, `discount`, `costPrice?`. `costPrice` có từ lúc đơn được duyệt.
- Danh sách đơn trả theo khuôn phân trang chung, đơn mới nhất trước, mỗi đơn kèm các dòng của nó. `from` và `to` lọc theo `createdAt`, tính cả hai đầu. `status` hoặc `channel` ngoài các giá trị của đơn, `page` nhỏ hơn 1 hoặc `pageSize` ngoài khoảng 1 đến 100 trả 400 `validation_failed`.
- `GET /orders/{id}` trả thêm `reservations[]`, mỗi phần giữ gồm `id`, `orderItemId`, `skuId`, `quantity`, `status` (`Active`, `Consumed`, `Released`), `expiresAt`, `closedAt?`. `id` không có trong tenant trả 404 `not_found`.
- Duyệt, hoàn tất, hủy: `id` không có trong tenant trả 404 `not_found`. Duyệt đơn không ở Reserved, hoàn tất đơn không ở Confirmed, hủy đơn đã Confirmed hoặc Completed trả 409 `invalid_state_transition` và không gì thay đổi.
- Hủy qua API luôn ghi `cancelReason = Manual` và không nhận lý do từ body; `Expired` chỉ do job hết hạn đặt.
- Tạo đơn: bỏ trống `unitPrice` thì dùng giá lẻ hiện tại của SKU; bỏ trống `discount` là 0. `items[]` rỗng, `quantity` không dương, `unitPrice` hoặc `discount` âm, `discount` vượt `quantity × unitPrice` của dòng đều trả 400 `validation_failed`.
- Tạo đơn: `branchId` hoặc `skuId` chưa có bản sao ở `core` trả 409 `reference_not_ready`. `core` không phân biệt được bản sao chưa tới với ID của tenant khác, nên ID của tenant khác cũng nhận mã này. Chi nhánh đã tắt hoặc SKU ngừng bán trả 409 `inactive_reference`, `details` gồm `reason` (`InactiveBranch` hoặc `InactiveSku`) và `id`.

## Đường vào không phải HTTP

| Nguồn | Tên | Xử lý |
| --- | --- | --- |
| RabbitMQ | `SubmitOrder` | Tạo đơn online và giữ hàng; phát `OrderReserved` hoặc `OrderRejected` |
| RabbitMQ | `SkuUpserted`, `BranchUpserted` | Ghi đè `sku_refs`, `branch_refs` theo `version` |
| Hangfire, mỗi phút | Hết hạn giữ hàng | Hủy đơn Reserved quá hạn, mỗi đơn một transaction |
