# ADR-0002: Chung schema, phân tách tenant bằng cột tenant_id

- Trạng thái: Đã xác nhận 2026-10-04
- Gộp từ: D-01
- Ngày: 2026-10-03

## Bối cảnh

NFR-TENANT-01 ghi "Shared Database, Separate Schema/Tenant ID", tức chưa chọn giữa schema riêng cho từng tenant và chung schema kèm cột định danh. Cùng yêu cầu đó lại đòi mọi bảng có `TenantId` và dùng EF Core Global Query Filters, là dấu hiệu của cách thứ hai.

## Quyết định

- Mọi tenant dùng chung schema trong database của từng service.
- Mọi bảng nghiệp vụ có cột `tenant_id`; mọi unique index và chỉ mục tra cứu bắt đầu bằng `tenant_id`.
- EF Core Global Query Filter áp cho mọi entity `ITenantOwned`; interceptor gán và kiểm `TenantId` khi ghi.

Cơ chế chi tiết ở [architecture/multi-tenancy.md](../architecture/multi-tenancy.md).

## Hệ quả

- Một bộ migration cho mọi tenant; thêm tenant không cần thao tác trên database.
- Cách ly dựa vào code. Một truy vấn bỏ filter sai chỗ là rò dữ liệu, nên phải có danh sách cố định các chỗ được bỏ filter và test T14, T15.
- Chỉ mục lớn dần theo tổng dữ liệu của mọi tenant; `tenant_id` đứng đầu chỉ mục để mỗi truy vấn chỉ quét phần của một tenant.

## Phương án đã loại

- Mỗi tenant một schema: cách ly mạnh hơn ở mức database, nhưng mỗi tenant mới phải chạy migration, và Global Query Filter trở nên thừa trong khi đề lại bắt buộc dùng nó.
- Mỗi tenant một database: vượt quá nhu cầu của đề.

## Nếu bị đổi

Sang schema riêng: sửa DbContext base trong `Oism.BuildingBlocks` để chọn schema theo tenant, viết công cụ chạy migration cho từng schema, sửa task W1-02 và mọi migration của 5 service.
