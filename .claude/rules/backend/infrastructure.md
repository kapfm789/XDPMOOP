---
paths:
  - "backend/**/*.Infrastructure/**"
---

# Lớp Infrastructure

Infrastructure cài đặt interface của Application bằng EF Core, PostgreSQL, RabbitMQ và Hangfire. Nguồn chuẩn: `docs/conventions/backend.md`, `docs/architecture/multi-tenancy.md`, `docs/design/data-model/`.

- Bảng, cột, khóa, chỉ mục phải khớp `docs/design/data-model/<service>.md`. Đổi schema thì sửa tài liệu đó trong cùng thay đổi.
- Ánh xạ bằng `IEntityTypeConfiguration<T>`; tên bảng và cột snake_case qua `UseSnakeCaseNamingConvention`.
- DbContext kế thừa DbContext base của `Oism.BuildingBlocks` để có Global Query Filter và interceptor gán `TenantId`.
- Mọi unique index và chỉ mục tra cứu bắt đầu bằng `tenant_id`.
- `IgnoreQueryFilters` chỉ được dùng trong các phương thức liệt kê ở `docs/architecture/multi-tenancy.md`. Cần thêm chỗ mới thì sửa tài liệu đó trước.
- Khóa dòng bằng `FromSqlInterpolated` với `FOR UPDATE`, có `ORDER BY` cố định. SQL thô luôn qua tham số.
- Migration: mỗi thay đổi một migration mới cho mỗi service; không sửa migration đã merge. CHECK và trigger viết bằng `migrationBuilder.Sql` trong migration tạo bảng.
- Không bật lazy loading. Truy vấn chỉ đọc dùng `AsNoTracking` và chiếu sang DTO.
- Job Hangfire chỉ gọi handler ở Application; job chạy trên nhiều tenant phải đặt tenant context cho từng đơn vị xử lý.
- Không đặt nghiệp vụ ở đây: không tính giá vốn, không quyết định chuyển trạng thái trong repository.
