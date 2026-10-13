# Luồng: kiểm kê

Nhập số đếm không tác động tồn; chỉ bước chốt mới ghi sổ. Chốt là một transaction cho cả phiên: hoặc mọi SKU được điều chỉnh, hoặc không SKU nào. Use case [UC-INV-04](../../usecase-userstory/inventory.md); quyết định [ADR-0009](../../decisions/0009-transfer-and-stocktake.md).

```mermaid
sequenceDiagram
  autonumber
  actor Staff
  participant Api as core Api
  participant UC as PostStocktakeHandler
  participant DB as schema core

  Staff->>Api: POST stocktakes/:id/post
  Api->>UC: PostStocktakeCommand
  UC->>DB: BEGIN
  UC->>DB: SELECT phiên FOR UPDATE
  alt phiên đã Posted
    UC->>DB: COMMIT, không đổi gì
    Api-->>Staff: 200, phiên hiện có
  else phiên đang Open
    UC->>DB: tạo dòng số dư còn thiếu của các SKU đã đếm
    UC->>DB: SELECT số dư các SKU đã đếm ORDER BY sku_id FOR UPDATE
    alt có SKU mà số đếm nhỏ hơn reserved
      UC->>DB: tìm các đơn đang giữ những SKU đó
      UC->>DB: ROLLBACK
      Api-->>Staff: 409 kèm danh sách đơn
    else mọi SKU hợp lệ
      UC->>UC: ghi system_qty và difference lên dòng phiên, phiên sang Posted
      loop từng SKU có chênh lệch, qua PostLedger
        UC->>UC: on_hand về số đếm, thêm dòng sổ IN hoặc OUT lý do StocktakeAdjust
      end
      UC->>UC: xếp StockChanged của từng SKU đã điều chỉnh vào outbox
      UC->>DB: lưu phiên, số dư, sổ và outbox, rồi COMMIT
      Api-->>Staff: 200
    end
  end
```

Hình dạng `details` của lỗi 409 và của phiên trả về nằm ở [api/core.md](../api/core.md) mục "Chuyển kho và kiểm kê".

## Mỗi bước đổi gì

| Bước | Khóa | Số dư | Sổ |
| --- | --- | --- | --- |
| Khóa phiên | Dòng `stocktakes` | | |
| Khóa số dư | Các dòng `inventory_balances` của SKU đã đếm, theo `sku_id` | | |
| SKU đếm nhiều hơn sổ | | `on_hand` tăng tới số đếm | IN, `reason = StocktakeAdjust`, số lượng là phần chênh |
| SKU đếm ít hơn sổ | | `on_hand` giảm tới số đếm | OUT, `reason = StocktakeAdjust`, số lượng là phần chênh |
| SKU đếm bằng sổ | | Không đổi | Không ghi |

`avg_cost` không đổi qua kiểm kê. `unit_cost` của dòng sổ điều chỉnh là `avg_cost` hiện tại.

## Vì sao chặn khi số đếm nhỏ hơn reserved

Nếu `reserved = 8` mà đếm được 6, đặt `on_hand = 6` sẽ làm tồn khả dụng thành âm 2 và vi phạm CHECK. Hệ thống không tự chọn đơn nào bị hủy. Nó trả danh sách đơn đang giữ SKU đó để nhân viên hủy hoặc xử lý, rồi chốt lại.

## Trường hợp biên

- `system_qty` là `on_hand` đọc được lúc chốt, dưới khóa, không phải lúc mở phiên. Bán hàng xảy ra giữa lúc đếm và lúc chốt vì thế được tính vào.
- SKU không được đếm thì không bị đụng tới.
- SKU chưa có dòng số dư mà được đếm lớn hơn 0: tạo dòng với tồn 0 rồi điều chỉnh tăng; giá vốn bằng 0 cho tới lần nhập mua đầu tiên.
- Chốt lại phiên đã Posted: không tác động lần hai.

## Kiểm thử

T13 (số đếm thấp hơn lượng đã giữ bị chặn).
