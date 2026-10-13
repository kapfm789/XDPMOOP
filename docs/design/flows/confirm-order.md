# Luồng: xác nhận đơn

Duyệt đơn là lúc hàng rời kho: một transaction trừ `on_hand`, trừ `reserved`, chốt giá vốn lên từng dòng đơn và ghi sổ. Use case [UC-ORD-03](../../usecase-userstory/orders.md); API `POST /api/core/orders/:id/confirm`; quyết định [ADR-0005](../../decisions/0005-order-lifecycle-and-transactions.md).

```mermaid
sequenceDiagram
  autonumber
  actor Staff
  participant Api as core Api
  participant UC as ConfirmOrderHandler
  participant Stock as IStockService
  participant DB as schema core

  Staff->>Api: POST orders/:id/confirm
  Api->>UC: ConfirmOrderCommand
  UC->>DB: BEGIN
  UC->>DB: SELECT đơn FOR UPDATE, rồi nạp các dòng đơn
  alt đơn không ở Reserved
    UC->>DB: ROLLBACK
    UC-->>Api: InvalidStateTransitionException
    Api-->>Staff: 409
  else đơn ở Reserved
    UC->>UC: đơn sang Confirmed, ghi confirmed_at
    UC->>Stock: Consume các phần giữ Active của đơn
    Stock->>DB: đọc các phần giữ Active của đơn
    Stock->>DB: SELECT số dư ORDER BY sku_id FOR UPDATE
    loop từng phần giữ
      Stock->>Stock: reserved giảm, Reservation sang Consumed
    end
    loop từng phần giữ, qua PostLedger
      Stock->>Stock: on_hand giảm, thêm dòng sổ OUT lý do Sale, unit_cost là avg_cost
    end
    Stock->>Stock: xếp StockChanged của từng SKU vào outbox
    Stock-->>UC: giá vốn đã dùng cho từng dòng
    UC->>UC: ghi cost_price lên từng dòng đơn, xếp OrderConfirmed vào outbox
    UC->>DB: lưu đơn, số dư, phần giữ, sổ và outbox, rồi COMMIT
    UC-->>Api: đơn Confirmed
    Api-->>Staff: 200
  end
```

Trạng thái được kiểm ngay sau khi khóa dòng đơn, trước khi chạm tới số dư: đơn không ở Reserved thì dừng ở đó. `Consume` bỏ giữ cho mọi phần giữ trước rồi mới gọi `PostLedger`, vì `PostLedger` chỉ cho xuất trong tồn khả dụng. Mọi thay đổi nằm trong bộ nhớ của use case cho tới lần lưu duy nhất ngay trước COMMIT.

## Mỗi bước đổi gì

| Bước | Khóa | Số dư | Sổ | Outbox |
| --- | --- | --- | --- | --- |
| Khóa đơn | Dòng `orders` | | | |
| Khóa số dư | Các dòng `inventory_balances` của đơn, theo `sku_id` | | | |
| Mỗi dòng đơn | | `on_hand` giảm, `reserved` giảm, `version` tăng | Một dòng OUT, `reason = Sale`, `unit_cost` là `avg_cost` lúc này | |
| Chốt giá vốn | | | | |
| Kết thúc | | | | `OrderConfirmed` kèm `costPrice` từng dòng, `StockChanged` |

Tồn khả dụng không đổi qua bước này: hàng đã bị loại khỏi tồn khả dụng từ lúc giữ. Chỉ trừ `on_hand` mà quên trừ `reserved` sẽ làm hệ thống giữ thừa hàng.

## Trường hợp biên

- Job hết hạn chạy cùng lúc: cả hai cùng khóa dòng đơn. Bên khóa được trước đổi trạng thái; bên sau thấy đơn không còn ở Reserved và dừng.
- `avg_cost` đọc sau khi khóa số dư, nên một phiếu nhập xác nhận ngay trước đó đã được tính vào.
- `cost_price` ghi đúng một lần. Không có use case nào cập nhật lại trường này.
- Lỗi ở bất kỳ bước nào: rollback toàn bộ; đơn vẫn ở Reserved và phần giữ vẫn Active.
- `id` không có trong tenant: trả 404 và không khóa gì.
- Một SKU nằm ở hai dòng đơn: hai phần giữ, hai dòng sổ OUT, một `StockChanged` mang số dư sau dòng sổ cuối.

## Kiểm thử

T05 (lỗi khi ghi sổ thì không gì thay đổi), T07 (duyệt đúng lúc job hết hạn chạy), T08 (hủy sau Confirmed bị từ chối), T10 (giá vốn của đơn cũ không đổi).
