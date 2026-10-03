# Yêu cầu phi chức năng

Đề có 11 yêu cầu phi chức năng thuộc 5 nhóm. Mỗi dòng ghi chiến thuật kiến trúc đáp ứng và cách kiểm; không yêu cầu nào được coi là đạt chỉ vì "đã thiết kế như vậy".

Task và kịch bản kiểm chứng của từng mã nằm ở RTM, [PLAN.md](../PLAN.md) mục 16.

| Mã | Yêu cầu | Chiến thuật | Cách kiểm |
| --- | --- | --- | --- |
| NFR-PERF-01 | p95 tìm SKU hoặc quét mã dưới 200 ms; tạo đơn và giao dịch ghi dưới 500 ms; báo cáo doanh thu dưới 2 giây với dưới 100.000 bản ghi | `core` giữ bản sao SkuRef để trả SKU kèm tồn trong một lần gọi; chỉ mục theo tenant; báo cáo đọc bảng tổng hợp ngày | Đo bằng k6, ghi cấu hình máy và lượng dữ liệu (T17) |
| NFR-PERF-02 | 50 đơn đồng thời mua đơn vị cuối cùng: tuyệt đối không âm tồn khả dụng | Khóa bi quan `SELECT ... FOR UPDATE` trên dòng số dư; CHECK `reserved <= on_hand` ở database | Test tích hợp trên PostgreSQL thật, chạy lặp (T01, T02) |
| NFR-TENANT-01 | Mọi bảng nghiệp vụ có `TenantId`; bắt buộc EF Core Global Query Filters | Xem [multi-tenancy.md](../architecture/multi-tenancy.md) | Dùng ID của tenant khác trên API, event, SignalR, job (T14, T15) |
| NFR-TENANT-02 | Chỉ mục `(TenantId, CreatedAt)` và `(TenantId, SKUId)` cho ledger và đơn | Chỉ mục khai báo trong migration; thêm `BranchId` vì tồn quản lý theo chi nhánh | `EXPLAIN` cho truy vấn ledger và đơn dùng index |
| NFR-SEC-01 | Mật khẩu băm BCrypt hoặc Argon2; HTTPS TLS 1.3; access token 60 phút; refresh token an toàn | Xem [security.md](../architecture/security.md) | Test đăng nhập và phân quyền; kiểm cấu hình TLS ở gateway |
| NFR-SEC-02 | Tạo đơn → giữ hàng → trừ hàng trong một transaction nguyên tử, rollback toàn bộ khi lỗi | Đơn và kho chung service `core`; mỗi use case một transaction; event qua outbox | Gây lỗi giữa chừng và kiểm không có gì thay đổi (T05, T23) |
| NFR-SEC-03 | Ledger chỉ thêm mới; cấm `UPDATE`, `DELETE`; sửa sai bằng giao dịch bù trừ | Trigger database từ chối sửa và xóa; không có API sửa ledger | Thử `UPDATE` và `DELETE` trực tiếp (T16) |
| NFR-USA-01 | 99,5% thời gian hoạt động trong khung 07:00 đến 22:00 | Health check và restart policy cho mọi container | Không đo được trong 5 tuần; chỉ kiểm cấu hình và kịch bản khởi động lại (T20) |
| NFR-USA-02 | POS hoàn tất thanh toán dưới 3 lần nhấp, hoặc quét mã rồi Enter; chạy trên desktop, tablet, thiết bị cầm tay | Một màn hình bán hàng, phím tắt, ô tìm kiếm luôn giữ focus | Đếm thao tác với một sản phẩm, tiền mặt, không giảm giá; thử ba kích thước màn hình |
| NFR-MAINT-01 | Clean Architecture: Domain, Application, Infrastructure, Presentation | Xem [layering.md](../architecture/layering.md) | Review hướng tham chiếu giữa các project |
| NFR-MAINT-02 | Độ bao phủ unit và integration test tối thiểu 80% cho ledger, giữ hàng, giá vốn | Đo theo dòng lệnh, gộp unit và integration, riêng ba module Inventory, Reservation, Costing | Báo cáo coverlet trong CI |

## Điểm đề chưa nói rõ

- Hai ngưỡng 500 ms và 2 giây không ghi phân vị; nhóm đo p95 cho cả ba ngưỡng.
- Mục tiêu 80% không ghi là dòng lệnh hay nhánh; nhóm chọn dòng lệnh.
- 99,5% không ghi kỳ đo. Nếu tính 30 ngày, mỗi ngày 15 giờ, thì được phép gián đoạn tối đa khoảng 2 giờ 15 phút.
