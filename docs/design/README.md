# Thiết kế chi tiết

Thư mục này là nguồn chuẩn cho bảng, API, event, trạng thái và trình tự của từng luồng. Các mục 5 đến 8 của [PLAN.md](../PLAN.md) chỉ là bản tóm tắt của những gì ghi ở đây.

| Phần | File | Nội dung |
| --- | --- | --- |
| Mô hình dữ liệu | [data-model/identity.md](data-model/identity.md), [catalog.md](data-model/catalog.md), [core.md](data-model/core.md), [channel.md](data-model/channel.md), [insights.md](data-model/insights.md) | Bảng, cột, khóa, chỉ mục của từng schema |
| API | [api/identity.md](api/identity.md), [catalog.md](api/catalog.md), [core.md](api/core.md), [channel.md](api/channel.md), [insights.md](api/insights.md) | Endpoint, vai trò, dữ liệu vào ra, mã lỗi |
| Event | [events.md](events.md) | 8 thông điệp giữa các service, từng trường |
| Trạng thái | [state-machines.md](state-machines.md) | Đơn, phần giữ hàng, phiếu nhập, chuyển kho, kiểm kê, webhook |
| Luồng | [flows/](flows/) | Sơ đồ trình tự của 8 luồng lõi |
| Dự báo | [forecast.md](forecast.md) | Thuật toán, tham số, backtest |
| Giao diện | [ui/admin.md](ui/admin.md), [ui/pos.md](ui/pos.md) | Màn hình, route, API mỗi màn hình gọi |

## Tám luồng lõi

| Luồng | File | Use case |
| --- | --- | --- |
| Nhập hàng và tính giá vốn | [flows/purchase-receipt.md](flows/purchase-receipt.md) | UC-INV-02 |
| Giữ hàng | [flows/reserve-stock.md](flows/reserve-stock.md) | UC-ORD-01, UC-ORD-02 |
| Xác nhận đơn | [flows/confirm-order.md](flows/confirm-order.md) | UC-ORD-03 |
| Hủy và hết hạn | [flows/cancel-expire.md](flows/cancel-expire.md) | UC-ORD-04, UC-ORD-05 |
| POS checkout | [flows/pos-checkout.md](flows/pos-checkout.md) | UC-POS-02 |
| Chuyển kho | [flows/stock-transfer.md](flows/stock-transfer.md) | UC-INV-03 |
| Kiểm kê | [flows/stocktake.md](flows/stocktake.md) | UC-INV-04 |
| Nhận webhook | [flows/webhook-ingestion.md](flows/webhook-ingestion.md) | UC-SIM-01, UC-ORD-01 |

## Quy ước chung

| Hạng mục | Quy ước |
| --- | --- |
| Tên bảng và cột | snake_case, bảng ở số nhiều; ánh xạ bằng EF Core ở lớp Infrastructure |
| Khóa chính | `id uuid` do ứng dụng sinh, trừ khi ghi khác |
| Tenant | Mọi bảng nghiệp vụ có `tenant_id uuid NOT NULL`; mọi chỉ mục bắt đầu bằng `tenant_id` |
| Thời gian | `timestamptz`, lưu UTC |
| Tiền, đơn giá, giá vốn | `numeric(18,4)` |
| Số lượng | `integer`, đơn vị là cái |
| Trạng thái và loại | Lưu dạng chuỗi tên enum, không lưu số |
| Xóa | Không xóa dữ liệu nghiệp vụ; dùng cờ `is_active`. Chỉ xóa được chứng từ còn Draft |
| JSON trên API | camelCase; thời gian ISO 8601 UTC |
| Sơ đồ | Mermaid trong Markdown |

## Khi sửa thiết kế

Đổi bảng, API hoặc event thì sửa file ở đây trong cùng PR với code. Đổi event còn phải theo quy tắc chỉ thêm trường ở [architecture/messaging.md](../architecture/messaging.md).
