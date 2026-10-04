# Bản ghi quyết định kiến trúc

Mỗi file ở đây ghi một quyết định cho điểm mà đề bài để ngỏ: bối cảnh, lựa chọn, hệ quả và cái phải sửa nếu lựa chọn bị đổi. Cả 13 bản ghi đã được giảng viên xác nhận ngày 2026-10-04 (task W1-01), đúng như nhóm đề xuất.

Bảng D-01 đến D-18 ở [PLAN.md](../PLAN.md) mục 3 là bản tóm tắt; các file ở đây là nguồn chuẩn.

| ADR | Quyết định | Gộp từ | Trạng thái |
| --- | --- | --- | --- |
| [0001](0001-microservices-by-transaction-boundary.md) | 5 service và gateway; đơn và kho chung `core`; event qua outbox | D-17, D-18 | Đã xác nhận 2026-10-04; phần database thay bằng 0013 |
| [0002](0002-shared-schema-tenant-id.md) | Chung schema, cột `tenant_id`, Global Query Filter | D-01 | Đã xác nhận 2026-10-04 |
| [0003](0003-ledger-balance-and-locking.md) | Ledger chỉ thêm mới kèm bảng số dư; khóa bi quan | D-02, D-03 | Đã xác nhận 2026-10-04 |
| [0004](0004-wac-per-branch.md) | Giá vốn bình quân theo chi nhánh và SKU | D-04 | Đã xác nhận 2026-10-04 |
| [0005](0005-order-lifecycle-and-transactions.md) | Confirmed là xuất kho; hủy trước Confirmed; ranh giới transaction; giữ hàng 30 phút | D-05, D-06, D-07, D-08 | Đã xác nhận 2026-10-04 |
| [0006](0006-channel-simulator-and-idempotency.md) | Chỉ simulator; ba hàng rào chống xử lý trùng | D-09, D-10 | Đã xác nhận 2026-10-04 |
| [0007](0007-pos-payment-no-offline.md) | QR xác nhận tay; không bán offline | D-11 | Đã xác nhận 2026-10-04 |
| [0008](0008-report-formulas.md) | Công thức doanh thu thuần, giá vốn, bán chạy, bán chậm | D-12 | Đã xác nhận 2026-10-04 |
| [0009](0009-transfer-and-stocktake.md) | Chuyển kho nhận đủ; kiểm kê bị chặn khi thấp hơn lượng đã giữ | D-13 | Đã xác nhận 2026-10-04 |
| [0010](0010-ai-forecast.md) | Dự báo thống kê, không dùng dịch vụ ngoài | D-14 | Đã xác nhận 2026-10-04 |
| [0011](0011-auth-and-roles.md) | JWT, refresh token, mỗi người dùng một tenant | D-15 | Đã xác nhận 2026-10-04 |
| [0012](0012-frontend-workspaces.md) | Một workspace npm: admin, pos, shared | D-16 | Đã xác nhận 2026-10-04 |
| [0013](0013-one-database-schema-per-service.md) | Một database `oism`, mỗi service một schema riêng | D-18 | Đã xác nhận 2026-10-04 |

## Cách dùng

- Viết code theo ADR như một ràng buộc đã chốt. Không lặng lẽ làm khác đi vì thấy cách khác hay hơn.
- Muốn đổi một quyết định: sửa trạng thái ADR cũ thành "Thay bằng ADR-xxxx", viết ADR mới theo cùng khuôn, cập nhật [PLAN.md](../PLAN.md) mục 3 và các tài liệu bị ảnh hưởng trong cùng PR.
- Khi giảng viên trả lời: đổi trạng thái thành "Đã xác nhận" hoặc "Bị đổi", kèm ngày.

## Khuôn của một ADR

```markdown
# ADR-00NN: <quyết định, viết thành một câu khẳng định>

- Trạng thái: Đề xuất | Đã xác nhận | Bị đổi | Thay bằng ADR-xxxx
- Gộp từ: D-xx
- Ngày: YYYY-MM-DD

## Bối cảnh
## Quyết định
## Hệ quả
## Phương án đã loại
## Nếu bị đổi
```
