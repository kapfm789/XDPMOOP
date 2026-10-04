# API: core

Tiền tố `/api/core`. Use case ở [inventory.md](../../usecase-userstory/inventory.md), [orders.md](../../usecase-userstory/orders.md), [pos.md](../../usecase-userstory/pos.md); bảng ở [data-model/core.md](../data-model/core.md); trình tự ở [flows/](../flows/). Quy ước chung ở [README.md](README.md).

## POS

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/core/pos/skus` | Cashier, Staff, Owner | `query`, `branchId` | Tối đa 20 SKU: `skuId`, `skuCode`, `name`, `barcodes`, `retailPrice`, `available` | |
| POST | `/api/core/pos/checkout` | Cashier, Owner | Header `Idempotency-Key`; `branchId`, `items[]`, `payment` | 201: đơn ở Completed kèm dòng đơn và thanh toán | 409 `insufficient_stock`; 403 khi Cashier sai chi nhánh |

`items[]`: `skuId`, `quantity`, `unitPrice?`, `discount?`. Bỏ trống `unitPrice` thì dùng giá lẻ hiện tại. `payment`: `method` (`Cash` hoặc `QR`), `amount`. Gửi lại cùng `Idempotency-Key` trả 200 với đơn đã tạo.

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
| POST | `/api/core/stocktakes` | Owner, Staff | `branchId` | 201: phiên Open | |
| PUT | `/api/core/stocktakes/{id}/counts` | Owner, Staff | `items[]` (`skuId`, `countedQty`) | 200: phiên | 409 khi đã Posted |
| POST | `/api/core/stocktakes/{id}/post` | Owner, Staff | | 200: phiên Posted kèm chênh lệch từng SKU | 409 `stocktake_below_reserved` |

## Đơn hàng

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/core/orders` | Owner, Staff | `status?`, `channel?`, `branchId?`, `from?`, `to?`, `page`, `pageSize` | Danh sách đơn | |
| GET | `/api/core/orders/{id}` | Owner, Staff | | Đơn kèm dòng đơn, phần giữ hàng, thanh toán | |
| POST | `/api/core/orders` | Owner, Staff | `branchId`, `note?`, `items[]` (`skuId`, `quantity`, `unitPrice?`, `discount?`) | 201: đơn Reserved, kênh Admin | 409 `insufficient_stock`, `reference_not_ready`, `inactive_reference` |
| POST | `/api/core/orders/{id}/confirm` | Owner, Staff | | 200: đơn Confirmed | 409 `invalid_state_transition` |
| POST | `/api/core/orders/{id}/complete` | Owner, Staff | | 200: đơn Completed | 409 `invalid_state_transition` |
| POST | `/api/core/orders/{id}/cancel` | Owner, Staff | `reason?` | 200: đơn Cancelled | 409 `invalid_state_transition` |

Hủy lại đơn đã Cancelled trả 200 với cùng đơn, không tác động lần hai. `costPrice` trên dòng đơn chỉ trả cho Owner.

- Đơn trả về gồm `id`, `orderNumber`, `branchId`, `channel`, `externalOrderId?`, `status`, `totalAmount`, `reservedUntil?`, `confirmedAt?`, `completedAt?`, `cancelledAt?`, `cancelReason?`, `note?`, `createdBy?`, `createdAt` và `items[]`; mỗi dòng gồm `id`, `skuId`, `skuCode`, `skuName`, `quantity`, `unitPrice`, `discount`.
- Tạo đơn: bỏ trống `unitPrice` thì dùng giá lẻ hiện tại của SKU; bỏ trống `discount` là 0. `items[]` rỗng, `quantity` không dương, `unitPrice` hoặc `discount` âm, `discount` vượt `quantity × unitPrice` của dòng đều trả 400 `validation_failed`.
- Tạo đơn: `branchId` hoặc `skuId` chưa có bản sao ở `core` trả 409 `reference_not_ready`. `core` không phân biệt được bản sao chưa tới với ID của tenant khác, nên ID của tenant khác cũng nhận mã này. Chi nhánh đã tắt hoặc SKU ngừng bán trả 409 `inactive_reference`, `details` gồm `reason` (`InactiveBranch` hoặc `InactiveSku`) và `id`.

## Đường vào không phải HTTP

| Nguồn | Tên | Xử lý |
| --- | --- | --- |
| RabbitMQ | `SubmitOrder` | Tạo đơn online và giữ hàng; phát `OrderReserved` hoặc `OrderRejected` |
| RabbitMQ | `SkuUpserted`, `BranchUpserted` | Ghi đè `sku_refs`, `branch_refs` theo `version` |
| Hangfire, mỗi phút | Hết hạn giữ hàng | Hủy đơn Reserved quá hạn, mỗi đơn một transaction |
