---
name: add-use-case
description: "Thêm một use case vào một service backend của OISM theo lát cắt dọc qua bốn lớp: quy tắc ở Domain, handler ở Application, repository ở Infrastructure, endpoint ở Api, kèm test cho từng tiêu chí chấp nhận. Dùng skill này khi người dùng nói 'thêm use case', 'thêm API', 'thêm endpoint', 'thêm chức năng', 'làm UC-ORD-04', hoặc đưa một mã use case (UC-...) hay một tiêu chí chấp nhận (AC-n) cần hiện thực, kể cả khi họ không gọi tên skill."
argument-hint: "[service] [mã use case, ví dụ core UC-ORD-04]"
---

# Thêm một use case

Yêu cầu: $ARGUMENTS

Một use case trong repo này luôn đi từ tiêu chí chấp nhận tới code, không đi ngược lại. Nếu hành vi cần thêm chưa có trong `docs/usecase-userstory/`, việc đầu tiên là thêm tiêu chí chấp nhận vào đó.

## 1. Chốt đầu vào

- Đọc use case trong `docs/usecase-userstory/`: story, từng tiêu chí chấp nhận, API và link thiết kế.
- Đọc endpoint tương ứng ở `docs/design/api/<service>.md`: phương thức, đường dẫn, vai trò, dữ liệu vào ra, mã lỗi.
- Đọc bảng liên quan ở `docs/design/data-model/<service>.md`.
- Nếu use case đổi trạng thái hoặc đổi tồn: đọc `docs/design/state-machines.md` và luồng ở `docs/design/flows/`. Với `core`, đọc thêm `docs/architecture/transactions-and-concurrency.md`.
- Thiếu endpoint, trường hoặc trường hợp biên trong tài liệu: bổ sung vào tài liệu trước và nêu cho người dùng biết đã bổ sung gì.

## 2. Viết test theo tiêu chí chấp nhận

Mỗi tiêu chí một test, tên `<Use case>_<Điều kiện>_<Kết quả>`, gắn `[Trait("UseCase", "UC-XXX-NN AC-n")]`.

| Tiêu chí thuộc loại | Viết ở |
| --- | --- |
| Quy tắc thuần, công thức, chuyển trạng thái | `UnitTests` |
| Hành vi qua API, quyền, mã lỗi | `IntegrationTests` |
| Tranh chấp giữa các request | `IntegrationTests/Concurrency` |
| Cách ly tenant | `IntegrationTests/Tenancy` |

## 3. Viết code từ trong ra ngoài

**Domain**: thêm phương thức nghiệp vụ lên entity hoặc lớp Domain. Bước chuyển trạng thái nằm ở đây và khớp bảng chuyển trạng thái. Lỗi nghiệp vụ là exception có mã.

**Application**: tạo thư mục `<Module>/<TênUseCase>/` với command hoặc query, handler và validator theo khuôn ở `docs/conventions/backend.md`. Handler:

1. Mở một transaction qua `IUnitOfWork`.
2. Nạp chứng từ bằng phương thức có khóa.
3. Gọi phương thức của Domain.
4. Đổi tồn chỉ qua `IStockService` hoặc `PostLedger`.
5. Xếp event vào outbox bằng `IEventPublisher.Enqueue`.
6. Commit và trả DTO.

Khai báo interface repository hoặc truy vấn mới ở đây nếu cần.

**Infrastructure**: cài đặt interface vừa khai báo. Thêm cấu hình EF Core và migration nếu có bảng hoặc cột mới, khớp `docs/design/data-model/`. Truy vấn có khóa dùng `FOR UPDATE` với `ORDER BY` cố định.

**Api**: thêm action vào controller, khai báo policy theo bảng quyền ở `docs/architecture/security.md`, gọi handler, trả kết quả. Đăng ký handler và validator vào DI.

## 4. Những điểm hay sai

- Kiểm trạng thái trước khi khóa chứng từ. Phải khóa trước rồi mới kiểm.
- Kiểm tồn khả dụng trước khi khóa số dư.
- Quên trường hợp gọi lặp: thao tác lặp phải trả kết quả cũ và không tác động lần hai.
- Trả entity ra ngoài thay vì DTO, làm lộ trường chỉ Owner được xem (giá vốn).
- Dùng ID từ request mà không tra qua repository có filter tenant.
- Đặt nghiệp vụ trong controller hoặc trong repository.

## 5. Chạy và báo cáo

Chạy build và test của service. Báo cáo theo khuôn của `implement-task`: file theo lớp, bảng tiêu chí chấp nhận kèm test, lệnh đã chạy, tài liệu đã sửa, việc còn dở. Không commit.
