---
name: review-against-docs
description: "Soát các thay đổi hiện tại của repo OISM so với tài liệu trong docs/: tiêu chí chấp nhận, thiết kế, bất biến tồn kho, cách ly tenant, phân lớp, hợp đồng event. Dùng skill này trước khi mở PR, sau khi một công cụ AI vừa viết code, hoặc khi người dùng nói 'review', 'soát lại', 'kiểm tra xem đúng docs chưa', 'code này có lệch tài liệu không', 'check trước khi merge'. Skill chỉ báo cáo điểm lệch; không tự sửa trừ khi được yêu cầu."
argument-hint: "[tùy chọn: mã task hoặc đường dẫn cần soát]"
---

# Soát thay đổi so với tài liệu

Phạm vi: $ARGUMENTS

Mục tiêu là tìm chỗ code và tài liệu lệch nhau trước khi chúng vào `main`. Trong repo này, lệch có hai hướng và cả hai đều là lỗi: code làm khác tài liệu, hoặc code làm đúng nhưng tài liệu chưa được cập nhật.

## 1. Lấy thay đổi

- Mặc định: `git status` và `git diff` so với `main`, gồm cả file chưa track.
- Nếu người dùng đưa mã task hoặc đường dẫn, giới hạn theo đó.
- Không có thay đổi nào thì nói vậy và dừng.

## 2. Xác định tài liệu chi phối

Với mỗi folder có thay đổi, tra bảng "Từ folder tới tài liệu" ở `AGENTS.md` rồi đọc đúng các tài liệu đó. Nếu biết mã task, đọc thêm dòng task ở `docs/PLAN.md` mục 10 và use case của nó.

## 3. Soát theo từng nhóm

**Tiêu chí chấp nhận**
- Mỗi tiêu chí của use case liên quan có test hoặc bước demo chưa.
- Có hành vi nào trong code mà không tiêu chí nào yêu cầu không.

**Thiết kế**
- Bảng, cột, khóa, chỉ mục trong migration khớp `docs/design/data-model/`.
- Endpoint, vai trò, dữ liệu vào ra, mã lỗi khớp `docs/design/api/`.
- Tên và trường của thông điệp khớp `docs/design/events.md`; không trường nào bị đổi tên, xóa hay đổi kiểu.
- Bước chuyển trạng thái khớp `docs/design/state-machines.md`.
- Thứ tự bước trong handler khớp sơ đồ ở `docs/design/flows/`.

**Bất biến tồn kho** (khi có thay đổi ở `backend/services/core`)
- `on_hand` chỉ đổi qua `PostLedger`; `reserved` chỉ đổi qua `IStockService`.
- Khóa chứng từ trước, rồi số dư theo thứ tự cố định; kiểm trạng thái và kiểm tồn sau khi khóa.
- Không `UPDATE` hay `DELETE` trên sổ.
- Một use case một transaction; không gửi RabbitMQ hay gọi HTTP trong transaction.
- Thao tác lặp không tác động lần hai.

**Cách ly tenant**
- Entity nghiệp vụ mới cài `ITenantOwned`; chỉ mục mới bắt đầu bằng `tenant_id`.
- Tìm `IgnoreQueryFilters`: mỗi chỗ phải nằm trong danh sách ở `docs/architecture/multi-tenancy.md`.
- Không nhận `tenantId` từ URL hay body.

**Phân lớp**
- Domain không tham chiếu package hạ tầng; Application không tham chiếu Infrastructure hay `DbContext`.
- Controller và consumer không chứa nghiệp vụ.
- Frontend: không gọi `fetch` ngoài `frontend/shared`; `admin` và `pos` không import của nhau.

**Quyền**
- Endpoint mới có policy khớp bảng ở `docs/architecture/security.md`.
- Trường chỉ Owner được xem không lọt ra cho vai trò khác.

**Phụ thuộc và vệ sinh**
- Package mới có trong danh sách ở `docs/conventions/backend.md` không.
- Không có bí mật, không có `DateTime.UtcNow` ngoài lớp cài `IClock`, không có `any` ở frontend.

**Tài liệu**
- Thay đổi về bảng, API, event đã đi kèm thay đổi ở `docs/design/` chưa.
- RTM ở `docs/PLAN.md` mục 16 còn đúng không.

## 4. Chạy test nếu chạy được

Chạy build và test của phần có thay đổi. Ghi rõ đã chạy gì và kết quả; không chạy được thì nói vì sao.

## 5. Báo cáo

Chỉ báo điều đã kiểm thấy trong diff, kèm vị trí. Xếp theo mức nặng.

```markdown
## Kết quả soát

**Phạm vi:** <task hoặc đường dẫn, số file>

| Mức | Vị trí | Điểm lệch | Tài liệu | Đề xuất |
| --- | --- | --- | --- | --- |
| Chặn | `đường/dẫn.cs:42` | Kiểm tồn trước khi khóa số dư | `docs/architecture/transactions-and-concurrency.md` | Chuyển bước kiểm xuống sau câu khóa |

**Tiêu chí chấp nhận chưa có test:** ...

**Tài liệu cần cập nhật:** ...

**Đã chạy:** <lệnh và kết quả>
```

Mức: "Chặn" là vi phạm bất biến, rò tenant, sai quyền, mất dữ liệu; "Nên sửa" là lệch thiết kế hoặc thiếu test; "Gợi ý" là phần còn lại. Không tìm thấy điểm lệch nào thì nói rõ đã soát những nhóm nào.

Không sửa code hay tài liệu trong skill này trừ khi người dùng yêu cầu sau khi đọc báo cáo.
