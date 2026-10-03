---
paths:
  - "backend/**/*.Application/**"
---

# Lớp Application

Application chứa use case. Mỗi use case là một thư mục gồm command hoặc query, handler và validator. Khuôn mẫu: `docs/conventions/backend.md`.

- Chỉ tham chiếu Domain và `Oism.Contracts`. Không tham chiếu Infrastructure, Api hay EF Core; không dùng `DbContext`.
- Truy cập dữ liệu qua interface khai báo ở đây (`I...Repository`, `I...Queries`, `IUnitOfWork`), cài đặt ở Infrastructure.
- Một handler mở đúng một transaction qua `IUnitOfWork` và commit ở cuối. Không commit nửa chừng, không bắt lỗi rồi commit.
- Nạp chứng từ bằng phương thức có khóa (`GetForUpdateAsync`) trước khi kiểm trạng thái.
- Phát event bằng `IEventPublisher.Enqueue`, tức ghi vào outbox trong cùng transaction. Không gửi thẳng lên RabbitMQ.
- Handler là lớp thường đăng ký vào DI. Không dùng MediatR.
- Kiểm đầu vào bằng FluentValidation; kiểm nghiệp vụ nằm ở Domain.
- Trả DTO, không trả entity. Ánh xạ viết tay.
- Lấy giờ qua `IClock`.
- Trước khi viết handler, đọc tiêu chí chấp nhận của use case ở `docs/usecase-userstory/` và luồng tương ứng ở `docs/design/flows/`.
