# Kịch bản kiểm thử bắt buộc

23 kịch bản dưới đây là điều kiện nghiệm thu: 21 lấy từ phần phân tích đề, 2 thêm cho kiến trúc microservice (T22, T23). Owner và phase của từng kịch bản nằm ở [PLAN.md](../PLAN.md) mục 12. Cách viết test ở [strategy.md](strategy.md).

Đường dẫn ở cột "Đặt ở" tính từ `backend/services/<service>/tests/`, trừ khi ghi khác.

## Giữ hàng và đơn

| Mã | Cho trước | Khi | Thì | Đặt ở |
| --- | --- | --- | --- | --- |
| T01 | Một SKU có `on_hand = 1`, `reserved = 0` | 50 đơn online đồng thời, mỗi đơn mua 1 | Đúng 1 đơn ở Reserved, 49 đơn bị từ chối; `reserved = 1`; tồn khả dụng bằng 0; một phần giữ Active | core, `IntegrationTests/Concurrency` |
| T02 | Một SKU còn 1 đơn vị khả dụng | POS checkout và một đơn online cùng lúc mua 1 | Đúng một bên thành công; tổng số được nhận không vượt 1 | core, `IntegrationTests/Concurrency` |
| T03 | Một webhook đã xử lý; một đơn POS đã tạo | Gửi lại cùng webhook; gửi lại checkout cùng `Idempotency-Key` | Không có đơn, phần giữ hay dòng sổ mới; phản hồi trả lại kết quả cũ | channel và core, `IntegrationTests` |
| T04 | Đơn 2 SKU; SKU thứ nhất đủ hàng, SKU thứ hai thiếu | Tiếp nhận đơn | Đơn bị từ chối; `reserved` của cả hai SKU không đổi; không có đơn được lưu | core, `IntegrationTests` |
| T05 | Đơn ở Reserved; cắm lỗi vào bước ghi sổ | Duyệt đơn | Đơn vẫn Reserved; số dư, phần giữ, sổ, `cost_price`, outbox đều như trước | core, `IntegrationTests` |
| T06 | Đơn ở Reserved | Hủy hai lần; rồi chạy job hết hạn | `reserved` giảm đúng một lần; lần hai và job không đổi gì | core, `IntegrationTests` |
| T07 | Đơn ở Reserved đã quá hạn | Duyệt đơn và job hết hạn chạy đồng thời | Đơn kết thúc ở đúng một trạng thái; không có chuyện vừa có dòng sổ OUT vừa có phần giữ Released | core, `IntegrationTests/Concurrency` |
| T08 | Đơn ở Confirmed | Hủy đơn | 409 `invalid_state_transition`; `reserved` và `on_hand` không đổi | core, `IntegrationTests` |

## Kho và giá vốn

| Mã | Cho trước | Khi | Thì | Đặt ở |
| --- | --- | --- | --- | --- |
| T09 | Đã nhập 10 đơn vị giá 100.000 | Xác nhận nhập 5 đơn vị giá 130.000 | `avg_cost = 110.000`; `on_hand = 15`; hai dòng sổ IN | core, `UnitTests` cho công thức và `IntegrationTests` cho luồng |
| T10 | Bán 2 đơn vị khi `avg_cost = 110.000`; đơn đã Confirmed | Nhập lô mới làm `avg_cost` thành 120.000, rồi xem báo cáo | `cost_price` của đơn cũ vẫn 110.000; lợi nhuận gộp của đơn cũ không đổi | core và insights, `IntegrationTests` |
| T11 | Phiếu nhập đã Confirmed | Xác nhận lại | Trả phiếu hiện có; tồn, giá vốn và sổ không đổi | core, `IntegrationTests` |
| T12 | Phiếu chuyển kho từ A sang B đang InTransit | Xem tồn của B; thử bán ở B | Tồn khả dụng của B chưa tăng; tổng A cộng đang chuyển cộng B không đổi | core, `IntegrationTests` |
| T13 | Một SKU có `reserved = 8` | Chốt kiểm kê với số đếm 6 | 409 `stocktake_below_reserved` kèm danh sách đơn; không SKU nào của phiên bị điều chỉnh | core, `IntegrationTests` |
| T16 | Sổ có ít nhất một dòng | Chạy `UPDATE` rồi `DELETE` trực tiếp trên `inventory_transactions` | Database ném lỗi cho cả hai lệnh; dòng sổ không đổi | core, `IntegrationTests` |

## Cách ly tenant

| Mã | Cho trước | Khi | Thì | Đặt ở |
| --- | --- | --- | --- | --- |
| T14 | Tenant A và tenant B đều có đơn, SKU, chi nhánh | A gọi mọi endpoint đọc và ghi với ID của B; A tạo đơn trỏ tới SKU hoặc chi nhánh của B | 404 cho mọi lời gọi; riêng ở `core`, chi nhánh hoặc SKU của B gửi trong body nhận 409 `reference_not_ready` ([multi-tenancy.md](../architecture/multi-tenancy.md) quy tắc 3); không quan hệ chéo nào được tạo; dữ liệu của B không đổi | Mọi service, `IntegrationTests/Tenancy` |
| T15 | Hai phiên SignalR, một của A, một của B | Một đơn online của A được giữ hàng | Phiên của A nhận `OrderCreated`; phiên của B không nhận gì | insights, `IntegrationTests` |

## Thông điệp

| Mã | Cho trước | Khi | Thì | Đặt ở |
| --- | --- | --- | --- | --- |
| T22 | Một `OrderConfirmed` đã được `insights` xử lý | Giao lại đúng thông điệp đó | `sales_facts` không thêm dòng; số liệu báo cáo không đổi | insights, `IntegrationTests/Messaging` |
| T23 | RabbitMQ đang tắt | Tạo một đơn giữ hàng, hoặc duyệt một đơn | Transaction commit; thông điệp nằm trong outbox; khi RabbitMQ chạy lại, thông điệp được gửi đúng một lượt | core, `IntegrationTests/Messaging` |

## Hiệu năng

| Mã | Cho trước | Khi | Thì | Đặt ở |
| --- | --- | --- | --- | --- |
| T17 | Gói Compose đã nạp seed; 100.000 dòng bán | Chạy ba kịch bản k6: tìm SKU, tạo đơn, báo cáo doanh thu | p95 dưới 200 ms, dưới 500 ms, dưới 2 giây; báo cáo ghi cấu hình đo | `tools/k6` |

## Dự báo

| Mã | Cho trước | Khi | Thì | Đặt ở |
| --- | --- | --- | --- | --- |
| T18 | 60 ngày lịch sử bán | Chạy backtest | Có WAPE của dự báo và của phương án cơ sở; không ngày nào sau mốc cắt được dùng | insights, `UnitTests` cho thuật toán và `IntegrationTests` cho job |
| T19 | Một SKU mới có 5 ngày lịch sử; một lần chạy bị cắm lỗi | Chạy dự báo, rồi xem đề xuất | SKU mới hiện chưa đủ dữ liệu; lần chạy lỗi ghi Failed và kết quả lần trước vẫn hiện; không phiếu nhập hay dòng sổ nào được tạo | insights, `IntegrationTests` |

## Gói bàn giao

| Mã | Cho trước | Khi | Thì | Đặt ở |
| --- | --- | --- | --- | --- |
| T20 | Máy chỉ có Docker; repo vừa clone | `docker compose up` theo hướng dẫn | 5 service, gateway, hai frontend, PostgreSQL, RabbitMQ đều khỏe; đăng nhập bằng tài khoản seed; chạy được kịch bản demo | Kiểm tay theo hướng dẫn cài đặt |
| T21 | Swagger và Postman collection | Gọi lần lượt các API theo tài liệu | Mã trạng thái và hình dạng phản hồi khớp [design/api/](../design/api/README.md) | Kiểm tay; Postman collection ở `docs/` |

## Liên kết với use case

| Kịch bản | Tiêu chí chấp nhận |
| --- | --- |
| T01 | UC-ORD-01 AC-5 |
| T02 | UC-POS-02 AC-4 |
| T03 | UC-ORD-01 AC-4, UC-POS-02 AC-3, UC-SIM-01 AC-4 |
| T04 | UC-ORD-01 AC-3 |
| T05 | UC-ORD-03 AC-4, UC-POS-02 AC-6 |
| T06 | UC-ORD-04 AC-2, UC-ORD-05 AC-2 |
| T07 | UC-ORD-03 AC-5 |
| T08 | UC-ORD-04 AC-3 |
| T09 | UC-INV-02 AC-3 |
| T10 | UC-ORD-03 AC-6, UC-REP-01 AC-4 |
| T11 | UC-INV-02 AC-5 |
| T12 | UC-INV-03 AC-3 |
| T13 | UC-INV-04 AC-4 |
| T14 | UC-AUTH-01 AC-3 |
| T15 | UC-SIM-02 AC-3, UC-REP-03 AC-4 |
| T16 | UC-INV-01 AC-4 |
| T17 | UC-POS-01 AC-6, UC-REP-01 AC-7 |
| T18 | UC-AI-02 AC-2 |
| T19 | UC-AI-01 AC-2, AC-3, AC-5 |
| T22 | UC-REP-01 AC-5 |
