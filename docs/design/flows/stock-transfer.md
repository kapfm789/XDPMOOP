# Luồng: chuyển kho

Chuyển kho gồm hai transaction cách nhau bằng thời gian vận chuyển: xuất ở nơi gửi và nhận ở nơi nhận. Giữa hai bước, hàng không thuộc tồn của chi nhánh nào mà nằm trên phiếu ở trạng thái InTransit. Use case [UC-INV-03](../../usecase-userstory/inventory.md); quyết định [ADR-0009](../../decisions/0009-transfer-and-stocktake.md).

```mermaid
sequenceDiagram
  autonumber
  actor Staff
  participant Api as core Api
  participant UC as Handler chuyển kho
  participant DB as schema core

  Staff->>Api: POST transfers/:id/ship
  Api->>UC: ShipTransferCommand
  UC->>DB: BEGIN
  UC->>DB: SELECT phiếu FOR UPDATE, rồi nạp các dòng phiếu
  alt phiếu không còn Draft
    UC->>DB: ROLLBACK
    Api-->>Staff: 409 invalid_state_transition
  else phiếu ở Draft
    UC->>UC: phiếu sang InTransit, ghi shipped_at
    UC->>DB: tạo dòng số dư còn thiếu ở nơi gửi
    UC->>DB: SELECT số dư nơi gửi ORDER BY sku_id FOR UPDATE
    alt một dòng xuất quá tồn khả dụng nơi gửi
      UC->>DB: ROLLBACK
      Api-->>Staff: 409 insufficient_stock
    else đủ hàng
      UC->>UC: PostLedger OUT lý do TransferOut cho từng dòng phiếu, unit_cost là avg_cost nơi gửi
      UC->>UC: ghi unit_cost lên dòng phiếu, xếp StockChanged vào outbox
      UC->>DB: lưu phiếu, số dư, sổ và outbox, rồi COMMIT
      Api-->>Staff: 200
    end
  end

  Note over Staff,DB: hàng đang vận chuyển, chưa bán được ở đâu

  Staff->>Api: POST transfers/:id/receive
  Api->>UC: ReceiveTransferCommand
  UC->>DB: BEGIN
  UC->>DB: SELECT phiếu FOR UPDATE, rồi nạp các dòng phiếu
  alt phiếu đã Received
    UC->>DB: COMMIT, không đổi gì
    Api-->>Staff: 200, phiếu hiện có
  else phiếu còn Draft
    UC->>DB: ROLLBACK
    Api-->>Staff: 409 invalid_state_transition
  else phiếu ở InTransit
    UC->>UC: phiếu sang Received, ghi received_at
    UC->>DB: tạo dòng số dư còn thiếu ở nơi nhận
    UC->>DB: SELECT số dư nơi nhận ORDER BY sku_id FOR UPDATE
    UC->>UC: PostLedger IN lý do TransferIn cho từng dòng phiếu: tính lại avg_cost nơi nhận bằng unit_cost trên phiếu, on_hand tăng
    UC->>UC: xếp StockChanged vào outbox
    UC->>DB: lưu phiếu, số dư, sổ và outbox, rồi COMMIT
    Api-->>Staff: 200
  end
```

Cả hai bước kiểm trạng thái của phiếu ngay sau khi khóa dòng phiếu, trước khi khóa số dư. Việc khóa số dư, kiểm tồn khả dụng, tính giá vốn và ghi sổ đều nằm trong `PostLedger`; handler chỉ dựng danh sách bút toán.

## Mỗi bước đổi gì

| Bước | Khóa | Số dư | Sổ |
| --- | --- | --- | --- |
| Xuất | Phiếu, rồi số dư nơi gửi | `on_hand` nơi gửi giảm; `avg_cost` nơi gửi không đổi | OUT, `reason = TransferOut`, `unit_cost` là `avg_cost` nơi gửi |
| Nhận | Phiếu, rồi số dư nơi nhận | `on_hand` nơi nhận tăng; `avg_cost` nơi nhận tính lại | IN, `reason = TransferIn`, `unit_cost` lấy từ dòng phiếu |

Cả hai dòng sổ có `reference_type = StockTransfer` và cùng `reference_id`, nên đối soát được hai đầu.

## Bất biến

Tại mọi thời điểm, với mỗi SKU: tồn nơi gửi cộng số lượng trên các phiếu InTransit cộng tồn nơi nhận không đổi do chuyển kho.

## Trường hợp biên

- Kiểm khi xuất dùng tồn khả dụng, không dùng `on_hand`: hàng đang giữ cho đơn không được chuyển đi. `details` của lỗi nêu dòng đầu tiên bị thiếu theo thứ tự xử lý, không gom mọi SKU thiếu như bước giữ hàng của đơn.
- `id` phiếu không có trong tenant: trả 404 và không khóa gì.
- Nhận lại phiếu đã Received: không tác động lần hai.
- Nhận phiếu còn Draft: trả 409.
- Phiếu Draft xóa được; phiếu InTransit không hủy được.
- Nơi nhận chưa có dòng số dư cho SKU: tạo dòng trước khi khóa; giá vốn nơi nhận bằng `unit_cost` trên phiếu.
- Nhận đủ số lượng đã xuất. Hàng thiếu hoặc hỏng xử lý bằng một phiên kiểm kê ở nơi nhận.

## Kiểm thử

T12 (chưa nhận thì tồn khả dụng nơi nhận chưa tăng).
