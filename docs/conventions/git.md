# Quy ước git và định nghĩa "xong"

Mỗi thay đổi gắn với một mã task ở [PLAN.md](../PLAN.md) mục 10. Nhánh, commit và PR đều mang mã đó để truy được từ yêu cầu tới code.

## Nhánh

- `main` luôn build được và chạy được.
- Nhánh làm việc: `feat/<mã task>-<mô tả ngắn bằng tiếng Anh>`, ví dụ `feat/W2-05-reservation-engine`.
- Sửa lỗi ngoài task: `fix/<mô tả>`. Chỉ sửa tài liệu: `docs/<mô tả>`.

## Commit

- Dòng đầu: `<mã task>: <việc đã làm>`, ví dụ `W2-05: engine giữ hàng khóa theo thứ tự SKU`.
- Mỗi commit build được. Không commit code bị comment bỏ, file tạm hay bí mật.
- Migration và code dùng nó nằm trong cùng commit.

## Pull request

- Một PR một task, hoặc một phần rõ ràng của task.
- Mô tả PR ghi: mã task, mã yêu cầu, tiêu chí chấp nhận đã đạt, test đã thêm, tài liệu đã sửa.
- Cần một người khác duyệt và CI xanh. A duyệt cho B, B duyệt cho A; PR của C do người sở hữu API liên quan duyệt.
- PR đổi `Oism.Contracts` cần cả dev bên phát và dev bên nhận duyệt.

## Định nghĩa "xong" cho mọi task

Một task xong khi đạt cột "Xong khi" của nó ở PLAN và đủ các điều kiện sau:

1. Tiêu chí chấp nhận của use case liên quan đều có test hoặc bước demo.
2. Có test cho quy tắc nghiệp vụ; phần kho và giữ hàng có test tích hợp trên PostgreSQL thật.
3. Mọi truy vấn đi qua Global Query Filter.
4. Endpoint có policy theo vai trò và hiện trên Swagger.
5. Không truy cập database của service khác; không gọi HTTP giữa các service.
6. Tài liệu ở `docs/design/` khớp với code vừa viết; đổi bảng, API hay event thì đã sửa tài liệu trong cùng PR.
7. RTM ở PLAN mục 16 đã cập nhật nếu task hoặc kịch bản kiểm chứng thay đổi.

## Nhịp làm việc

- Đầu mỗi phase chia task; cuối mỗi phase demo 15 phút để kiểm cổng.
- Track nào chưa qua cổng của phase thì chưa mở task phase sau của track đó.
- Quyết định mới hoặc thay đổi quyết định cũ ghi thành ADR ở [decisions/](../decisions/README.md) trước khi viết code theo nó.
