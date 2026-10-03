---
paths:
  - "deploy/**"
  - ".github/**"
  - "**/Dockerfile"
---

# Triển khai và CI

Nguồn chuẩn: `docs/plans/deploy.md`, `docs/architecture/context-and-containers.md`.

- Tên service trong Compose, tiền tố route và cổng theo bảng "Địa chỉ và cổng" ở `docs/architecture/context-and-containers.md`. Không tự đổi.
- Một PostgreSQL với một database `oism` và 5 schema: `identity`, `catalog`, `core`, `channel`, `insights`, tạo bằng `deploy/postgres/init-schemas.sql`. Mọi service nhận cùng chuỗi kết nối tới `oism` và chỉ đọc ghi schema của mình (`docs/decisions/0013-one-database-schema-per-service.md`).
- Mọi container có health check và restart policy.
- Không đưa bí mật vào repo. Giá trị mẫu nằm ở `.env.example`; `.env` phải nằm trong `.gitignore`.
- Khóa bí mật ký JWT chỉ cấp cho `identity`; gateway và các service khác chỉ nhận khóa công khai.
- TLS kết thúc ở gateway. Chỉ gateway, `admin` và `pos` mở cổng ra ngoài mạng Compose ở gói demo.
- Seed chạy lặp lại được mà không tạo dữ liệu trùng, và không chạy ở môi trường không phải demo hay dev.
- CI build và test backend, build frontend; test tích hợp cần Docker cho Testcontainers.
- Không thêm Kubernetes, service mesh hay công cụ hạ tầng khác; ngoài phạm vi (xem `docs/PLAN.md` mục 2).
