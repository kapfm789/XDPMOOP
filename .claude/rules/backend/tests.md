---
paths:
  - "backend/**/tests/**"
---

# Test backend

Nguồn chuẩn: `docs/testing/strategy.md` cho cách viết, `docs/testing/scenarios.md` cho 23 kịch bản bắt buộc.

- Test đơn vị ở `Oism.<Service>.UnitTests`; test tích hợp ở `Oism.<Service>.IntegrationTests`, chạy trên PostgreSQL thật bằng Testcontainers.
- Không dùng database in-memory hay SQLite cho bất kỳ test nào đụng tới `core`: chúng không có `FOR UPDATE`, CHECK và trigger như PostgreSQL.
- Mỗi test tích hợp tự tạo tenant riêng và chỉ đọc dữ liệu của tenant đó. Không phụ thuộc thứ tự chạy, không dùng chung dữ liệu.
- Tên test: `<Use case>_<Điều kiện>_<Kết quả>`. Gắn `[Trait("Scenario", "Txx")]` hoặc `[Trait("UseCase", "UC-XXX-NN AC-n")]`.
- Test đồng thời giữ mọi request sau một cổng rồi thả cùng lúc; đặt ở `IntegrationTests/Concurrency`.
- Test của `core` kết thúc bằng kiểm bất biến tồn kho: số dư khớp sổ, `reserved` khớp phần giữ Active, không âm.
- Test nguyên tử: cắm lỗi vào giữa transaction rồi kiểm mọi bảng liên quan giữ nguyên, gồm cả outbox.
- Thời gian đi qua `IClock` giả; không ngủ chờ đồng hồ thật.
- So sánh bằng `Assert` của xUnit; không thêm thư viện assert.
- Không sửa test cho xanh bằng cách nới điều kiện. Test đỏ vì code sai thì sửa code; đỏ vì yêu cầu đổi thì sửa tiêu chí chấp nhận trong `docs/usecase-userstory/` trước.
