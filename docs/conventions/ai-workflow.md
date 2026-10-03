# Làm việc với công cụ AI

Repo được dựng để công cụ AI viết code theo tài liệu thay vì tự đoán: tài liệu nói phải làm gì, source tree nói đặt ở đâu, rule và skill nói làm theo trình tự nào. Việc của người dùng là giao đúng một task, rồi kiểm kết quả theo tiêu chí chấp nhận.

## AI đọc những gì

| File | Ai đọc | Nội dung |
| --- | --- | --- |
| `AGENTS.md` ở gốc repo | Mọi công cụ đọc AGENTS.md; Claude Code đọc qua `CLAUDE.md` | Quy tắc chung, bản đồ tài liệu, bất biến không được phá |
| `CLAUDE.md` | Claude Code | Nạp `AGENTS.md`, thêm phần riêng của Claude Code |
| `.claude/rules/*.md` | Claude Code | Rule tự nạp khi mở file ở đường dẫn khớp |
| `.claude/skills/*/SKILL.md` | Claude Code; công cụ khác đọc như tài liệu quy trình | Quy trình từng bước, gọi bằng `/tên-skill` |
| `docs/` | Mọi công cụ, theo chỉ dẫn của AGENTS.md | Yêu cầu, thiết kế, quy ước |

## Skill có sẵn

| Lệnh | Dùng khi | Ví dụ |
| --- | --- | --- |
| `/implement-task <mã task>` | Làm trọn một task trong kế hoạch | `/implement-task W2-05` |
| `/scaffold-service <tên>` | Dựng skeleton một service bốn lớp | `/scaffold-service catalog` |
| `/add-use-case <service> <mã use case>` | Thêm một use case đi xuyên bốn lớp | `/add-use-case core UC-ORD-04` |
| `/add-event <tên event>` | Thêm hoặc đổi một thông điệp giữa các service | `/add-event OrderConfirmed` |
| `/review-against-docs` | Soát thay đổi hiện tại so với tài liệu trước khi mở PR | `/review-against-docs` |

Với công cụ không có skill, yêu cầu nó đọc file `SKILL.md` tương ứng rồi làm theo.

## Cách giao việc

Giao một task một lần và gọi bằng mã.

- Nên: "Làm task W2-05" hoặc `/implement-task W2-05`.
- Nên: "Thêm tiêu chí chấp nhận UC-ORD-04 AC-2 và test của nó."
- Không nên: "Làm phần đơn hàng đi." Phạm vi quá rộng, AI sẽ tự đoán ranh giới.
- Không nên: "Làm cả phase 3." Ba dev ba track; mỗi task cần được kiểm riêng.

Khi yêu cầu nằm ngoài tài liệu, sửa tài liệu trước (thêm tiêu chí chấp nhận, thêm endpoint vào `design/api/`), rồi mới bảo AI viết code. Làm ngược lại thì code và tài liệu lệch nhau ngay từ đầu.

## Kiểm kết quả của AI

1. Đối chiếu từng tiêu chí chấp nhận của use case: có test hoặc bước demo cho từng cái chưa.
2. Chạy test. Với `core`, test tích hợp phải chạy trên PostgreSQL thật.
3. Mở diff và soát các điểm dễ sai: thứ tự khóa, `IgnoreQueryFilters`, event gửi ngoài outbox, nghiệp vụ nằm ở controller.
4. Kiểm tài liệu đã được sửa cùng nếu bảng, API hay event thay đổi.
5. Chạy `/review-against-docs` để có danh sách lệch giữa code và tài liệu.

## Khi AI làm sai lặp lại

Sửa nguồn chứ đừng nhắc đi nhắc lại trong hội thoại.

| Hiện tượng | Sửa ở đâu |
| --- | --- |
| Sai một quy tắc áp cho mọi nơi | `AGENTS.md` |
| Sai khi đụng một loại file cụ thể | Rule tương ứng trong `.claude/rules/` |
| Bỏ sót bước trong một quy trình | `SKILL.md` của skill đó |
| Hiểu sai nghiệp vụ | Tài liệu trong `docs/`: thêm tiêu chí chấp nhận hoặc trường hợp biên |

Giữ `AGENTS.md` dưới 200 dòng. Chi tiết chỉ cần cho một phần của code thì đặt vào rule theo đường dẫn hoặc vào `docs/`.

## Giới hạn

- Rule và skill hiện là bản đầu, viết trước khi repo có code. Chúng chưa được chạy thử trên code thật; sau task W1-02 nên chạy thử `/implement-task` với một task nhỏ rồi chỉnh lại.
- AI không tự commit, không tự đẩy code và không tự thêm package ngoài danh sách ở [backend.md](backend.md); các việc đó cần người xác nhận.
