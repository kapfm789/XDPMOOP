---
paths:
  - "backend/**/*.Domain/**"
---

# Lớp Domain

Domain chứa nghiệp vụ thuần để test được mà không cần database hay web server. Nguồn chuẩn: `docs/architecture/layering.md`.

- Không tham chiếu EF Core, ASP.NET Core, RabbitMQ, Hangfire hay bất kỳ package hạ tầng nào. Entity không mang attribute của EF Core.
- Quy tắc nghiệp vụ là phương thức của entity hoặc của một lớp Domain: `order.Confirm(...)`, `WeightedAverageCost.Recalculate(...)`. Không để handler tự đổi trạng thái bằng cách gán thuộc tính.
- Bước chuyển trạng thái phải khớp bảng ở `docs/design/state-machines.md`; bước không có trong bảng ném `InvalidStateTransitionException`.
- Lỗi nghiệp vụ là exception của Domain có mã, theo bảng ở `docs/conventions/backend.md`.
- Không dùng `DateTime.UtcNow`; nhận thời gian qua tham số.
- Entity nghiệp vụ cài `ITenantOwned`.
- Tiền là `decimal`, số lượng là `int`; không dùng `double` hay `float`.
- Mỗi quy tắc mới có test đơn vị ở `tests/Oism.<Service>.UnitTests`.
