# Transaction, khóa và bất biến tồn kho

Tài liệu này là phần phải đúng tuyệt đối của `core`. Mọi thay đổi tồn kho xảy ra trong một transaction database, dưới khóa dòng, và để lại đúng một dòng ledger. Code nào đổi tồn mà không theo ba điều đó là sai, kể cả khi test hiện có vẫn xanh.

Từng luồng cụ thể có sơ đồ ở [design/flows/](../design/flows/).

## Chín bất biến

Mỗi bất biến phải đúng sau mọi transaction đã commit.

| # | Bất biến | Được giữ bằng |
| --- | --- | --- |
| 1 | `available = on_hand - reserved`, `reserved >= 0`, `available >= 0` | CHECK trên `inventory_balances`; kiểm trong Domain trước khi ghi |
| 2 | `on_hand` bằng tổng IN trừ tổng OUT của ledger cho cùng tenant, chi nhánh, SKU | Chỉ `PostLedger` được đổi `on_hand` |
| 3 | `reserved` bằng tổng số lượng của các phần giữ hàng đang Active | Chỉ `IStockService` được đổi `reserved`, luôn đi kèm đổi trạng thái `Reservation` |
| 4 | Mỗi lần chuyển trạng thái gây tác động tồn chỉ tạo tác động đó một lần | Khóa dòng `orders` rồi mới kiểm trạng thái |
| 5 | Dòng ledger đã ghi không bao giờ bị sửa hay xóa | Trigger database; sửa sai bằng dòng `Reversal` |
| 6 | `CostPrice` của dòng đơn đã xác nhận không đổi khi có đợt nhập mới | Ghi một lần lúc xác nhận, không có đường cập nhật |
| 7 | Mọi bản ghi trong cùng một nghiệp vụ thuộc cùng tenant | Global Query Filter và interceptor khi ghi |
| 8 | Nghiệp vụ lỗi giữa chừng không để lại gì | Một use case một transaction; event nằm trong outbox của cùng transaction |
| 9 | Hàng chuyển kho: tồn nơi gửi cộng hàng đang vận chuyển cộng tồn nơi nhận không đổi | Phiếu chuyển kho giữ số lượng đang vận chuyển giữa hai lần ghi ledger |

## Ranh giới transaction

| Use case | Số transaction | Gồm |
| --- | --- | --- |
| Xác nhận phiếu nhập | 1 | Khóa phiếu, khóa số dư, tính WAC, ghi ledger, outbox |
| Giữ hàng cho đơn online hoặc đơn thủ công | 1 | Chèn đơn, khóa số dư, kiểm tồn, tăng `reserved`, tạo `Reservation`, outbox |
| Xác nhận đơn | 1 | Khóa đơn, khóa số dư, trừ tồn, chốt giá vốn, ghi ledger, outbox |
| Hủy đơn hoặc hết hạn giữ hàng | 1 | Khóa đơn, khóa số dư, giảm `reserved`, outbox |
| POS checkout | 1 | Toàn bộ chuỗi giữ, xác nhận, thanh toán, hoàn tất |
| Chuyển kho | 2 | Xuất ở nơi gửi; nhận ở nơi nhận |
| Chốt kiểm kê | 1 | Khóa số dư, ghi bút toán điều chỉnh |

Đơn online đi qua nhiều transaction trong vòng đời của nó vì giữa các bước có người duyệt. Đây là cách nhóm đọc NFR-SEC-02 ([ADR-0005](../decisions/0005-order-lifecycle-and-transactions.md)).

## Thứ tự khóa

Mọi transaction lấy khóa theo cùng một thứ tự để không chờ nhau vòng tròn.

1. Dòng chứng từ: `orders`, `purchase_receipts`, `stock_transfers` hoặc `stocktakes`, bằng `SELECT ... FOR UPDATE`.
2. Các dòng `inventory_balances`, sắp theo `branch_id` rồi `sku_id` tăng dần, bằng một câu `SELECT ... ORDER BY ... FOR UPDATE`.

Không bao giờ khóa số dư trước rồi mới khóa chứng từ.

```sql
-- Tạo dòng số dư nếu SKU chưa từng có ở chi nhánh, để lúc khóa không bị hụt.
INSERT INTO inventory_balances (tenant_id, branch_id, sku_id, on_hand, reserved, avg_cost)
VALUES (@tenant, @branch, @sku, 0, 0, 0)
ON CONFLICT (tenant_id, branch_id, sku_id) DO NOTHING;

-- Khóa mọi dòng cần dùng trong một câu lệnh, theo thứ tự cố định.
SELECT *
FROM inventory_balances
WHERE tenant_id = @tenant AND branch_id = @branch AND sku_id = ANY(@skus)
ORDER BY branch_id, sku_id
FOR UPDATE;
```

Mức cô lập là READ COMMITTED mặc định của PostgreSQL, kèm khóa dòng tường minh. Không dùng SERIALIZABLE vì khi đó mọi use case phải tự thử lại khi xung đột.

## Một cửa duy nhất để đổi tồn

| Thao tác | Đổi gì | Ghi ledger | Ai được gọi |
| --- | --- | --- | --- |
| `PostLedger(IN hoặc OUT, lý do, số lượng, đơn giá, chứng từ)` | `on_hand`, và `avg_cost` khi là nhập mua hoặc nhận chuyển kho | Có, một dòng | Use case của Inventory; `IStockService` |
| `IStockService.Reserve` | `reserved` tăng; tạo `Reservation` Active | Không | Use case của Orders |
| `IStockService.Consume` | `reserved` giảm; gọi `PostLedger(OUT, Sale)`; `Reservation` sang Consumed; trả giá vốn đã dùng | Có | Use case của Orders |
| `IStockService.Release` | `reserved` giảm; `Reservation` sang Released | Không | Use case của Orders |

Bút toán xuất theo giá vốn (bán hàng, xuất chuyển kho) gọi `PostLedger` với đơn giá bỏ trống: `unit_cost` của dòng sổ khi đó là `avg_cost` của dòng số dư, đọc dưới khóa. `Consume` giảm `reserved` trước rồi mới gọi `PostLedger`, vì `PostLedger` chỉ cho xuất trong tồn khả dụng.

`BalanceAfter` của dòng ledger được tính khi đang giữ khóa dòng số dư, nên thứ tự theo `seq` trong cùng tenant, chi nhánh, SKU luôn nhất quán. Không sắp ledger theo `created_at` để suy ra thứ tự.

## Chống xử lý trùng

| Tình huống | Hàng rào | Hành vi khi trùng |
| --- | --- | --- |
| Webhook gửi lại | Unique `(tenant_id, channel, event_id)` ở `channel` | Trả 202 với trạng thái cũ |
| Cùng đơn online tới hai lần | Unique `(tenant_id, channel, external_order_id)` ở `core` | Bỏ qua, không giữ hàng lần hai |
| Bấm thanh toán hai lần ở POS | Unique `(tenant_id, idempotency_key)` | Trả lại đơn đã tạo |
| Xác nhận lại phiếu nhập | Khóa phiếu rồi kiểm trạng thái | Trả kết quả cũ |
| Hủy hai lần, job hết hạn chạy lại | Khóa đơn rồi kiểm trạng thái | Không làm gì |
| Event tới hai lần | `inbox_messages` | Bỏ qua |

Khi vi phạm unique xảy ra giữa hai request đồng thời, request thua rollback, đọc lại bản ghi đã có và trả về bản ghi đó.

## Hàng rào cuối ở database

```sql
ALTER TABLE inventory_balances
  ADD CONSTRAINT ck_balance_non_negative
  CHECK (on_hand >= 0 AND reserved >= 0 AND reserved <= on_hand);

CREATE FUNCTION forbid_ledger_change() RETURNS trigger AS $$
BEGIN
  RAISE EXCEPTION 'inventory_transactions is append-only';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_ledger_append_only
BEFORE UPDATE OR DELETE ON inventory_transactions
FOR EACH ROW EXECUTE FUNCTION forbid_ledger_change();
```

Hai hàng rào này không thay cho kiểm tra ở Domain. Chúng tồn tại để một lỗi lập trình thành lỗi rõ ràng thay vì thành số liệu sai.

## Những cách làm bị cấm

- Đọc tồn, kiểm ở ứng dụng, rồi ghi mà không khóa dòng. Hai request cùng thấy còn 1 sẽ cùng giữ 1.
- Đổi `on_hand` hoặc `reserved` bằng câu lệnh riêng ngoài `PostLedger` và `IStockService`.
- Gửi RabbitMQ, gọi HTTP hay in ấn bên trong transaction.
- Giữ transaction mở qua nhiều request.
- Bắt lỗi rồi commit phần đã làm. Lỗi thì rollback hết.
- Dùng database in-memory hay SQLite để test các luồng này. Chúng không có `FOR UPDATE` như PostgreSQL.

## Ghi chú cho EF Core

- Khóa dòng bằng `FromSqlInterpolated` với `FOR UPDATE`, trong transaction do `IUnitOfWork` mở.
- Bọc cả transaction trong execution strategy của Npgsql nếu bật retry; không để retry chạy lại nửa transaction.
- Sau khi khóa, đọc lại giá trị từ kết quả câu khóa; không dùng entity đã nạp trước đó trong cùng DbContext.
