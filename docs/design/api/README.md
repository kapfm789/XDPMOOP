# API: quy ước chung

Mọi API đi qua gateway theo dạng `/api/<service>/...`; gateway bỏ tiền tố trước khi chuyển tới service. Các file trong thư mục này ghi đường dẫn đầy đủ như frontend gọi. Swagger của từng service là tài liệu sinh từ code; khi Swagger và file ở đây khác nhau thì phải sửa một trong hai trong cùng PR.

| Service | File |
| --- | --- |
| identity | [identity.md](identity.md) |
| catalog | [catalog.md](catalog.md) |
| core | [core.md](core.md) |
| channel | [channel.md](channel.md) |
| insights | [insights.md](insights.md) |

## Xác thực

- Header `Authorization: Bearer <access token>` cho mọi endpoint, trừ các endpoint ghi "Công khai".
- Cột "Vai trò" liệt kê vai trò được gọi. Bảng quyền gốc ở [architecture/security.md](../../architecture/security.md).
- `tenantId` không bao giờ nằm trong URL hay body; server lấy từ token.

## Dữ liệu

- JSON, tên trường camelCase.
- Thời gian theo ISO 8601 UTC, ví dụ `2026-10-20T03:15:27Z`.
- Tiền là số thập phân; số lượng là số nguyên.
- Danh sách có phân trang nhận `page` (từ 1) và `pageSize` (mặc định 20, tối đa 100), trả `{ "items": [], "page": 1, "pageSize": 20, "total": 0 }`.

## Lỗi

Lỗi trả theo ProblemDetails, kèm trường `code` để giao diện xử lý.

```json
{
  "type": "https://oism.local/errors/insufficient_stock",
  "title": "Không đủ tồn khả dụng",
  "status": 409,
  "code": "insufficient_stock",
  "details": [{ "skuId": "0f9e8d7c-6b5a-4c3d-9e2f-1a0b9c8d7e6f", "requested": 2, "available": 1 }]
}
```

| Mã HTTP | `code` | Khi nào |
| --- | --- | --- |
| 400 | `validation_failed` | Dữ liệu vào sai; `errors` liệt kê từng trường |
| 401 | `unauthenticated` | Thiếu token, token hết hạn, sai mật khẩu |
| 401 | `invalid_signature` | Chữ ký webhook sai |
| 403 | `forbidden` | Vai trò không đủ quyền |
| 404 | `not_found` | Không có, hoặc thuộc tenant khác |
| 409 | `duplicate` | Trùng mã SKU, mã vạch, mã chi nhánh, email |
| 409 | `invalid_state_transition` | Bước chuyển trạng thái không hợp lệ |
| 409 | `insufficient_stock` | Không đủ tồn khả dụng; `details` nêu từng SKU |
| 409 | `stocktake_below_reserved` | Số đếm thấp hơn lượng đã giữ; `details` nêu các đơn |
| 409 | `reference_not_ready` | SKU hoặc chi nhánh chưa tới service do event trễ; thử lại sau |

## Chống gửi trùng

`POST /api/core/pos/checkout` bắt buộc có header `Idempotency-Key` là một uuid do POS sinh cho mỗi lần bấm thanh toán. Gửi lại cùng khóa trả lại đúng đơn đã tạo với mã 200.

## Khuôn bảng endpoint

Cột "Vào" và "Ra" chỉ ghi trường chính. Trường có dấu `?` là không bắt buộc.
