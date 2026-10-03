---
paths:
  - "backend/services/core/**"
---

# Bất biến của core

`core` là phần phải đúng tuyệt đối: mọi thay đổi tồn kho nằm trong một transaction, dưới khóa dòng, và để lại đúng một dòng sổ. Đọc `docs/architecture/transactions-and-concurrency.md` và luồng tương ứng ở `docs/design/flows/` trước khi sửa bất kỳ thứ gì ở đây.

- `on_hand` chỉ đổi qua `PostLedger`. `reserved` chỉ đổi qua `IStockService` (`Reserve`, `Consume`, `Release`). Không có câu lệnh nào khác được đụng hai cột này.
- Module Orders gọi Inventory qua `IStockService`; Orders không tự sửa `InventoryBalance` và không tự ghi sổ.
- Thứ tự khóa cố định: dòng chứng từ (`orders`, `purchase_receipts`, `stock_transfers`, `stocktakes`) trước, rồi các dòng `inventory_balances` theo `branch_id`, `sku_id` tăng dần, trong một câu `SELECT ... ORDER BY ... FOR UPDATE`.
- Kiểm trạng thái sau khi đã khóa chứng từ, không phải trước.
- Kiểm tồn khả dụng (`on_hand - reserved`) sau khi đã khóa số dư. Đọc rồi kiểm mà không khóa là lỗi bán vượt tồn.
- Giữ hàng không ghi sổ. Xác nhận đơn giảm cả `on_hand` lẫn `reserved`. Hủy chỉ giảm `reserved`.
- `cost_price` ghi một lần lúc xác nhận, bằng `avg_cost` đọc dưới khóa. Không có đường cập nhật lại.
- Sổ chỉ thêm mới. Không viết code `UPDATE` hay `DELETE` lên `inventory_transactions`.
- Các thao tác lặp lại (xác nhận lại phiếu, hủy lại đơn, job chạy lại, cùng `Idempotency-Key`) trả kết quả cũ và không tác động lần hai.
- Không gửi RabbitMQ, gọi HTTP hay in ấn bên trong transaction.
- Mỗi luồng có test tích hợp trên PostgreSQL thật, kết thúc bằng kiểm bất biến tồn kho. Kịch bản bắt buộc: `docs/testing/scenarios.md` (T01 đến T13, T16, T23).
