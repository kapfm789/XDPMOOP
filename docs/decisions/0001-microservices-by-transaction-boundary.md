# ADR-0001: Tách 5 service theo ranh giới transaction, nối bằng event qua outbox

- Trạng thái: Đã xác nhận 2026-10-04; dòng "mỗi service một database riêng" thay bằng [ADR-0013](0013-one-database-schema-per-service.md)
- Gộp từ: D-17, D-18
- Ngày: 2026-10-03

## Bối cảnh

Đề yêu cầu "Clean Architecture" và ".NET 8 Web API", không nhắc microservice. Nhóm muốn tách service để chia việc rõ và cô lập tải webhook. Đồng thời NFR-SEC-02, FR-POS-04 và FR-COST-02 buộc tạo đơn, giữ hàng, trừ hàng và chốt giá vốn phải nằm trong một transaction database.

## Quyết định

- Hệ thống gồm 5 service: `identity`, `catalog`, `core`, `channel`, `insights`, đứng sau một `gateway`.
- Đơn hàng và tồn kho nằm chung service `core`, chung một database, chia thành hai module Inventory và Orders.
- Mỗi service một database riêng trên cùng một PostgreSQL. Không join và không khóa ngoại chéo database.
- Service không gọi HTTP sang nhau. Dữ liệu dùng chung đi bằng event qua RabbitMQ, phát bằng transactional outbox, nhận bằng inbox.

## Hệ quả

- Giữ đúng chữ của NFR-SEC-02 mà vẫn có nhiều service.
- Tốn khoảng 22 trên 75 ngày công cho hạ tầng phân tán; nhóm việc ưu tiên P2 phải làm mức tối thiểu.
- Dữ liệu tham chiếu (SKU, chi nhánh), báo cáo và thông báo trễ theo event.
- Mỗi service phải tự áp cơ chế tenant và tự kiểm JWT, qua thư viện dùng chung.

## Phương án đã loại

- Một API duy nhất (modular monolith): rẻ nhất và đúng chữ đề nhất, nhưng không cô lập được tải webhook và không chia được ranh giới sở hữu theo service.
- 8 service, tách cả Orders khỏi Inventory: phải thay transaction bằng saga, trái NFR-SEC-02 và tốn khoảng 36 ngày công.

Phân tích đầy đủ ở [architecture/options-analysis.md](../architecture/options-analysis.md).

## Nếu bị đổi

- Giảng viên yêu cầu monolith: gộp 5 service thành một API, bỏ gateway và RabbitMQ; event giữa các service thành lời gọi trong tiến trình. Lấy lại khoảng 22 ngày công. Sửa [architecture/](../architecture/README.md), [design/events.md](../design/events.md), task W1-03 và W1-13.
- Cuối phase 4 chưa qua cổng: gộp `catalog` và `channel` vào `core`.
