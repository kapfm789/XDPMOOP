# backend/services/core — kế hoạch folder

`core` giữ mọi thứ phải commit cùng nhau: ledger, số dư, giá vốn, đơn hàng, giữ hàng, POS checkout (FR-INV, FR-COST, FR-RSE, FR-ORD, FR-POS-02, FR-POS-04). Owner: Dev A (module Inventory), Dev B (module Orders). Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Phân lớp

```text
core/
├─ src/
│  ├─ Oism.Core.Domain/
│  │  ├─ References/    SkuRef, BranchRef
│  │  ├─ Inventory/     InventoryBalance, InventoryTransaction, PurchaseReceipt, StockTransfer, Stocktake, WeightedAverageCost
│  │  └─ Orders/        Order, OrderItem, Reservation, Payment, OrderStateMachine
│  ├─ Oism.Core.Application/
│  │  ├─ References/    UpsertSkuRef, UpsertBranchRef
│  │  ├─ Inventory/     PostLedger, ConfirmPurchaseReceipt, ShipTransfer, ReceiveTransfer, PostStocktake
│  │  └─ Orders/        ReserveStock, ConfirmOrder, CancelOrder, PosCheckout, ExpireReservations
│  ├─ Oism.Core.Infrastructure/   CoreDbContext, migration, trigger chặn UPDATE/DELETE ledger, outbox, inbox, Hangfire
│  └─ Oism.Core.Api/              controller, consumer SkuUpserted, BranchUpserted, SubmitOrder
└─ tests/
   ├─ Oism.Core.UnitTests/           WAC, máy trạng thái, quy tắc giữ hàng
   └─ Oism.Core.IntegrationTests/    Testcontainers PostgreSQL, test đồng thời, rollback
```

Hai module dùng chung một DbContext và một transaction. Module Orders chỉ đổi tồn qua interface của module Inventory, không tự sửa `InventoryBalance`.

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 2 | W1-08 | core: skeleton, consumer `SkuUpserted` và `BranchUpserted` vào SkuRef, BranchRef | B | NFR-TENANT-01 | Gửi lại cùng event không tạo bản ghi trùng |
| 3 | W2-01 | core/Inventory: `InventoryTransaction` chỉ thêm mới (trigger chặn UPDATE và DELETE), `InventoryBalance`, hàm `PostLedger` | A | FR-INV-01, NFR-SEC-03 | UPDATE/DELETE bị database từ chối; OnHand bằng tổng ledger |
| 3 | W2-04 | core/Orders: Canonical Order, OrderItem, máy trạng thái, API tạo đơn thủ công | B | FR-ORD-01, FR-ORD-02 | Bước chuyển trạng thái sai bị từ chối |
| 4 | W2-02 | core/Inventory: phiếu nhập nháp rồi xác nhận, tăng OnHand, tính lại WAC | A | FR-INV-02, FR-COST-01 | 10 × 100.000 rồi 5 × 130.000 ra 110.000; xác nhận lại không đổi tồn |
| 4 | W2-03 | core: chỉ mục (TenantId, CreatedAt) cho ledger và đơn, (TenantId, BranchId, SkuId, Seq) cho ledger, (TenantId, SkuId) cho dòng đơn | A | NFR-TENANT-02 | Migration có chỉ mục; EXPLAIN dùng index |
| 4 | W2-05 | core/Orders: engine giữ hàng, khóa FOR UPDATE theo thứ tự SkuId, giữ toàn bộ hoặc từ chối | B | FR-RSE-01, FR-RSE-02, NFR-PERF-02 | 50 request đồng thời cho 1 sản phẩm: đúng 1 thành công |
| 4 | W2-06 | core: phát `OrderReserved`, `OrderRejected`, `StockChanged` qua outbox | B | NFR-SEC-02 | Event chỉ phát khi transaction commit; RabbitMQ tắt vẫn commit được |
| 4 | W2-08 | admin: phiếu nhập, tồn theo chi nhánh và ngưỡng tồn, xem ledger; core: API đặt ngưỡng tồn | C | FR-INV-01, FR-INV-02, FR-REP-03 | Nhập hàng từ giao diện, thấy dòng ledger và giá vốn mới; đặt được ngưỡng tồn |
| 5 | W3-05 | core/Inventory: chuyển kho hai bước, mang giá vốn | A | FR-INV-03 | B chưa nhận thì available của B không tăng; tổng A + đang chuyển + B không đổi |
| 5 | W3-01 | core/Orders: xác nhận đơn (trừ tồn, chốt `CostPrice`, ghi ledger), hoàn tất, hủy từ Reserved | B | FR-RSE-03, FR-COST-02, FR-ORD-03, NFR-SEC-02 | Lỗi giữa chừng rollback hết; hủy hai lần chỉ giải phóng một lần |
| 5 | W3-02 | core: POS checkout một transaction, `Idempotency-Key`, endpoint tìm SKU kèm available | B | FR-POS-02, FR-POS-04, NFR-SEC-02 | Bấm thanh toán hai lần chỉ một đơn; POS và online tranh đơn vị cuối không vượt tồn |
| 6 | W3-06 | core/Inventory: kiểm kê, bút toán điều chỉnh, chặn khi số đếm nhỏ hơn reserved | A | FR-INV-04 | Điều chỉnh tạo dòng ledger; ca thiếu bị chặn kèm danh sách đơn |
| 6 | W3-04 | core: job Hangfire hủy đơn Reserved hết hạn | B | FR-SIM-03 | Xác nhận đúng lúc job chạy: chỉ một kết quả thắng |
| 9 | W5-01 | Bù test tới ít nhất 80% dòng lệnh cho Inventory, Reservation, Costing | A, B | NFR-MAINT-02 | Có báo cáo coverlet |
| 9 | W5-02 | Bộ test cách ly tenant toàn hệ thống: API, event, SignalR, job | A | NFR-TENANT-01 | Mọi ca dùng ID tenant khác nhận 404 hoặc 403 |

## Sở hữu

- Schema `core` của database `oism`: SkuRef, BranchRef, InventoryBalance, InventoryTransaction, Supplier, PurchaseReceipt, StockTransfer, Stocktake, Order, OrderItem, Reservation, Payment (mục 5 của kế hoạch tổng).
- API: `/pos/skus`, `/pos/checkout`, `/stock`, `/ledger`, `/purchase-receipts`, `/transfers`, `/stocktakes`, `/orders` (mục 8).
- Event nhận: `SkuUpserted`, `BranchUpserted`, `SubmitOrder`. Event phát: `OrderReserved`, `OrderRejected`, `OrderConfirmed`, `OrderCancelled`, `StockChanged` (mục 7).
- Kiểm thử: T01 đến T13, T16, T23.

## Quy tắc riêng

- Mọi thay đổi `OnHand` đi qua `PostLedger`: ghi dòng ledger và cập nhật số dư trong cùng transaction (D-02).
- Khóa dòng số dư bằng `SELECT ... FOR UPDATE`, luôn theo thứ tự SkuId tăng dần (D-03).
- Ledger chỉ thêm mới; sửa sai bằng dòng `Reversal` trỏ về dòng gốc (NFR-SEC-03).
- Bảy luồng và ranh giới transaction của từng luồng nằm ở mục 6 của kế hoạch tổng; code phải khớp từng bước ở đó.
- Độ bao phủ kiểm thử tối thiểu 80% dòng lệnh cho Inventory, Reservation, Costing (NFR-MAINT-02).
