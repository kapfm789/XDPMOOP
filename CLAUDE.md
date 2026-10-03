@AGENTS.md

## Riêng cho Claude Code

- Rule theo đường dẫn nằm ở `.claude/rules/` và tự nạp khi mở file khớp. Chúng tóm tắt tài liệu trong `docs/`; khi rule và tài liệu khác nhau thì tài liệu thắng và rule cần được sửa.
- Skill của dự án nằm ở `.claude/skills/`:

| Lệnh | Dùng khi |
| --- | --- |
| `/implement-task <mã task>` | Làm trọn một task trong `docs/PLAN.md` |
| `/scaffold-service <tên service>` | Dựng skeleton một service bốn lớp |
| `/add-use-case <service> <mã use case>` | Thêm một use case đi xuyên bốn lớp |
| `/add-event <tên event>` | Thêm hoặc đổi một thông điệp giữa các service |
| `/review-against-docs` | Soát thay đổi hiện tại so với tài liệu trước khi mở PR |

- Khi được giao việc viết code mà không kèm mã task, tra mã task ở `docs/PLAN.md` trước, rồi làm theo `/implement-task`.
- Hướng dẫn cho người dùng về cách giao việc và kiểm kết quả: `docs/conventions/ai-workflow.md`.
