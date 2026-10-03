---
paths:
  - "docs/**"
  - "AGENTS.md"
  - "CLAUDE.md"
---

# Sửa tài liệu

`docs/` là nguồn chuẩn của dự án, nên sửa tài liệu cần cẩn thận như sửa code. Bản đồ và thứ tự ưu tiên giữa các tài liệu: `docs/README.md`.

- Viết bằng tiếng Việt; tên định danh trong code giữ tiếng Anh và đặt trong dấu backtick.
- Mỗi sự thật chỉ có một nguồn chuẩn. Nơi khác chỉ tóm tắt và trỏ link về nguồn. Đừng chép cùng một bảng sang hai file.
- Giữ nguyên các mã định danh: yêu cầu (`FR-`, `NFR-`), use case (`UC-`), tiêu chí (`AC-n`), task (`W1-01`), kịch bản (`T01`), quyết định (`D-01`, `ADR-0001`). Không đánh số lại mã đã có; thêm mới thì dùng số kế tiếp.
- Thêm hành vi mới: thêm tiêu chí chấp nhận ở `docs/usecase-userstory/` trước, rồi tới thiết kế ở `docs/design/`, rồi mới tới code.
- Đổi bảng, API, event: sửa file tương ứng trong `docs/design/` cùng lúc với code. Đổi event còn phải theo quy tắc chỉ thêm trường.
- Đổi một quyết định: không sửa nội dung ADR cũ; đặt trạng thái "Thay bằng ADR-xxxx" và viết ADR mới theo khuôn ở `docs/decisions/README.md`. Cập nhật `docs/PLAN.md` mục 3.
- Đổi task, owner hoặc phase: sửa `docs/PLAN.md` mục 10 và file tương ứng trong `docs/plans/`; hai nơi phải khớp từng chữ ở dòng task.
- Thêm hoặc bỏ yêu cầu, task, kịch bản: cập nhật RTM ở `docs/PLAN.md` mục 16.
- Sơ đồ viết bằng Mermaid ngay trong Markdown. Link giữa các tài liệu dùng đường dẫn tương đối và phải mở được.
- Không ghi một việc là "đã làm" hay "đã kiểm" khi chưa có code hoặc chưa chạy. Tài liệu thiết kế mô tả đích; trạng thái thực tế ghi ở `docs/README.md` mục "Trạng thái".
- Giữ `AGENTS.md` dưới 200 dòng; chi tiết chỉ áp cho một phần code thì đặt vào `.claude/rules/` hoặc `docs/`.
