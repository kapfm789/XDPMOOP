---
name: implement-task
description: "Thực hiện trọn một task trong kế hoạch OISM theo mã task (W1-01 đến W5-09), đi từ tài liệu tới test rồi tới code. Dùng skill này bất cứ khi nào người dùng nhắc tới một mã task, hoặc nói 'làm task', 'code task', 'lam task W2-05', 'implement W3-02', 'làm tiếp phase', kể cả khi họ không gọi tên skill. Cũng dùng khi được giao một việc viết code trong repo này mà chưa rõ thuộc task nào, để tra ra task trước khi viết."
argument-hint: "[mã task, ví dụ W2-05]"
---

# Thực hiện một task của kế hoạch OISM

Task cần làm: $ARGUMENTS

Repo này làm việc theo nguyên tắc tài liệu trước, code sau. Mọi task đã có sẵn yêu cầu, tiêu chí chấp nhận và thiết kế trong `docs/`. Việc của bạn là đọc đúng các tài liệu đó rồi viết code khớp với chúng, không phải tự thiết kế lại. Làm theo thứ tự dưới đây; mỗi bước tồn tại vì bỏ nó thì code hay lệch khỏi tài liệu.

## 1. Xác định task

- Tìm dòng bắt đầu bằng `| <mã task> |` trong `docs/PLAN.md` mục 10. Ghi lại: phase, việc, owner, mã yêu cầu, cột "Xong khi".
- Không có mã task: tìm task khớp mô tả của người dùng. Nếu khớp nhiều task hoặc không khớp task nào, hỏi lại trước khi làm.
- Chỉ làm đúng một task. Việc thuộc task khác mà bạn thấy cần thì ghi vào báo cáo, đừng làm luôn.

## 2. Đọc tài liệu, theo thứ tự

Chỉ đọc phần liên quan tới task; đừng nạp cả thư mục.

1. Plan của folder sẽ sửa: `docs/plans/<folder với dấu gạch>.md`. Xem mục "Việc theo phase", "Sở hữu", "Quy tắc riêng".
2. Use case mang mã yêu cầu của task: tra ở `docs/usecase-userstory/README.md`, rồi đọc đúng use case đó. Các tiêu chí chấp nhận đánh số là đích phải đạt.
3. Thiết kế của phần sẽ sửa trong `docs/design/`: data model, API, luồng (`flows/`), event, máy trạng thái của service đó.
4. Với `core`: `docs/architecture/transactions-and-concurrency.md`. Đọc hết phần bất biến và thứ tự khóa.
5. ADR liên quan trong `docs/decisions/` khi task chạm một điểm đề chưa chốt.
6. Kịch bản kiểm thử gắn với task ở `docs/testing/scenarios.md`.

Nếu tài liệu thiếu thứ task cần (một endpoint, một trường, một trường hợp biên) hoặc hai tài liệu nói khác nhau: dừng, nêu rõ cho người dùng và đề xuất cách sửa tài liệu. Đừng đoán rồi viết tiếp, vì code đoán sẽ lệch với phần người khác viết theo tài liệu.

## 3. Kiểm tiền đề

- Folder và project theo `docs/architecture/source-tree.md` đã có chưa. Chưa có skeleton thì task W1-02 phải làm trước; báo lại thay vì tự dựng một cấu trúc khác.
- Task dựa vào kết quả của task nào (ví dụ giữ hàng cần mô hình đơn và bảng số dư). Thiếu thì báo.
- Cổng của phase trước đã qua chưa, nếu người dùng có nói tới.

## 4. Nêu kế hoạch ngắn

Trước khi viết, liệt kê trong 5 đến 10 dòng: file sẽ tạo hoặc sửa theo từng lớp, test sẽ viết, tài liệu sẽ phải cập nhật. Rồi làm tiếp ngay; chỉ dừng chờ khi ở bước 2 có điểm thiếu hoặc mâu thuẫn.

## 5. Viết test trước

- Mỗi tiêu chí chấp nhận có ít nhất một test, hoặc một bước demo nếu đó là hành vi giao diện.
- Kịch bản `Txx` gắn với task phải thành test thật, có `[Trait("Scenario", "Txx")]`.
- Cách viết và nơi đặt theo `docs/testing/strategy.md`. Luồng đụng tới tồn kho dùng test tích hợp trên PostgreSQL thật.

## 6. Viết code theo lớp

Đi từ trong ra ngoài: Domain, Application, Infrastructure, Api. Với frontend: `shared`, `features`, `pages`.

- Khuôn handler, controller, xử lý lỗi: `docs/conventions/backend.md`.
- Tên, đường dẫn: `docs/architecture/source-tree.md`.
- Bảng và cột khớp `docs/design/data-model/`; endpoint khớp `docs/design/api/`; event khớp `docs/design/events.md`.
- Thứ tự bước trong handler khớp sơ đồ ở `docs/design/flows/`.

## 7. Chạy và sửa

- Chạy build và test của phần vừa sửa. Sửa tới khi xanh.
- Không sửa test cho xanh bằng cách nới điều kiện.
- Không chạy được (chưa có Docker, chưa có skeleton) thì nói rõ đã chạy gì, chưa chạy gì và vì sao. Đừng viết "đã kiểm" cho thứ chưa chạy.

## 8. Tự soát trước khi báo cáo

Đối chiếu thay đổi với danh sách này:

- [ ] Mỗi tiêu chí chấp nhận có test hoặc bước demo.
- [ ] Không vi phạm mục "Bất biến không được phá" ở `AGENTS.md`.
- [ ] Không có `IgnoreQueryFilters` ngoài các chỗ được phép.
- [ ] Không có nghiệp vụ trong controller, consumer hay repository.
- [ ] Event đi qua outbox; consumer ghi inbox.
- [ ] Endpoint mới có policy theo vai trò.
- [ ] Bảng, API, event thay đổi thì `docs/design/` đã được sửa.
- [ ] Không thêm package ngoài danh sách ở `docs/conventions/backend.md`.

## 9. Báo cáo

Dùng đúng khuôn này:

```markdown
## Task <mã>: <tên việc>

**Yêu cầu:** <mã FR/NFR>

**File đã tạo hoặc sửa**
- Domain: ...
- Application: ...
- Infrastructure: ...
- Api: ...
- Test: ...

**Tiêu chí chấp nhận**
| Tiêu chí | Kết quả | Test |
| --- | --- | --- |
| UC-XXX-NN AC-1 | Đạt | `TênTest` |

**Đã chạy:** <lệnh và kết quả>

**Tài liệu đã sửa:** <file, hoặc "không có">

**Còn dở và giả định:** <việc chưa làm, điểm chưa kiểm được, giả định đã đặt>
```

Không commit và không push; người dùng tự quyết định sau khi xem báo cáo.
