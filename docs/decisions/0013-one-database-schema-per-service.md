# ADR-0013: Một database PostgreSQL dùng chung, mỗi service một schema riêng

- Trạng thái: Đề xuất, chờ giảng viên xác nhận
- Gộp từ: D-18
- Thay: dòng "mỗi service một database riêng" của [ADR-0001](0001-microservices-by-transaction-boundary.md); các dòng còn lại của ADR-0001 giữ nguyên
- Ngày: 2026-10-03

## Bối cảnh

ADR-0001 cho mỗi service một database riêng trên cùng một PostgreSQL. NFR-TENANT-01 của đề ghi "Shared Database". Cụm đó nói về việc các tenant dùng chung database chứ không nói về service, nên 5 database không trái đề; nhưng nhóm sẽ phải giải thích điểm này khi bảo vệ, và phải vận hành 5 database cho một gói demo chạy trên một máy.

Cái cần giữ từ ADR-0001 là ranh giới sở hữu dữ liệu giữa các service, không phải số database.

## Quyết định

- Cả hệ thống dùng một database PostgreSQL tên `oism`.
- Mỗi service sở hữu một schema trùng tên service: `identity`, `catalog`, `core`, `channel`, `insights`.
- Service chỉ đọc và ghi schema của mình. Không join, không khóa ngoại, không view chéo schema. Dữ liệu dùng chung vẫn đi bằng event qua outbox và inbox như ADR-0001.
- DbContext của mỗi service đặt schema mặc định bằng `HasDefaultSchema`; bảng lịch sử migration `__EFMigrationsHistory` nằm trong schema của chính service đó.
- Hangfire của `core` dùng schema `hangfire_core`, của `insights` dùng schema `hangfire_insights`.
- `deploy/postgres/init-schemas.sql` tạo 5 schema. Mọi service nhận cùng một chuỗi kết nối tới `oism`.

## Hệ quả

- Hệ thống chỉ có một database, nên chữ "Shared Database" của NFR-TENANT-01 đúng mà không cần giải thích thêm.
- Một database để sao lưu, khôi phục và mở ra xem khi demo.
- Ranh giới transaction không đổi: mỗi service vẫn mở kết nối và transaction riêng, không transaction nào trải qua hai schema. Đơn và kho vẫn commit cùng nhau vì cùng ở schema `core`.
- Rào chống đọc chéo yếu đi: trước đây service không có chuỗi kết nối tới database khác, nay mọi service cùng một chuỗi kết nối nên việc không đọc schema khác dựa vào quy ước và review.
- Bảng trùng tên giữa các service (`outbox_messages`, `inbox_messages`) không đụng nhau vì khác schema. Bảng lịch sử migration và bảng Hangfire phải đặt schema tường minh như ở mục Quyết định; để mặc định thì các service dùng chung một bảng và lẫn dữ liệu của nhau.
- Mô hình tenant ở [ADR-0002](0002-shared-schema-tenant-id.md) không đổi. Schema ở đây chia theo service, không chia theo tenant; chỗ ADR-0002 ghi "database của từng service" nay đọc là "schema của từng service".
- Chi phí phân tán ước lượng 22 ngày công không đổi.

## Phương án đã loại

- Giữ 5 database như ADR-0001: rào chống đọc chéo mạnh hơn, nhưng phải giải thích chữ "Shared Database" và vận hành 5 database.
- Một database, service đọc thẳng bảng của nhau: bỏ được `sku_refs`, `branch_refs` và hai event upsert, nhưng các service dính nhau qua bảng; vẫn phải triển khai 5 service mà không còn ranh giới dữ liệu giữa chúng.
- Mỗi service một role PostgreSQL chỉ có quyền trên schema của mình: chặn đọc chéo ở mức database, đổi lại thêm 5 role và phần cấp quyền trong script khởi tạo. Chưa làm; thêm khi review bắt gặp truy vấn chéo schema.
- Modular monolith: vẫn là đường lui của ADR-0001 nếu giảng viên không nhận microservice.

## Nếu bị đổi

- Quay về mỗi service một database: đổi chuỗi kết nối và script khởi tạo. Không có tham chiếu chéo schema nên không phải sửa code nghiệp vụ.
- Sang modular monolith: theo mục "Nếu bị đổi" của ADR-0001; 5 schema giữ lại làm ranh giới module.
