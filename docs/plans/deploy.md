# deploy — kế hoạch folder

Docker Compose, khởi tạo database, seed data và CI cho gói demo (mục f của đề). Owner: Dev C; Dev A dựng Compose cho máy dev ở phase 1. Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Thuộc tầng nào

Tầng dữ liệu và hạ tầng chạy (mục 4.1 của kế hoạch tổng): PostgreSQL, RabbitMQ và cách các container nối với nhau.

## Cấu trúc

```text
deploy/
├─ docker-compose.yml             gateway, 5 service, admin, pos, PostgreSQL, RabbitMQ
├─ postgres/init-databases.sql    tạo 5 database: oism_identity, oism_catalog, oism_core, oism_channel, oism_insights
└─ seed/                          2 tenant, mỗi tenant 2 chi nhánh, khoảng 50 SKU, 60 ngày lịch sử bán

.github/workflows/ci.yml          build, test, build image
```

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 1 | W1-03 | Docker Compose dev: PostgreSQL với 5 database, RabbitMQ, gateway YARP | A | NFR-SEC-01 | `docker compose up` chạy; health check 5 service qua gateway |
| 1 | W1-11 | CI: GitHub Actions build và test backend, build frontend | C | Mục e.7 của đề | PR hiện check xanh hoặc đỏ |
| 8 | W4-09 | Seed data: 2 tenant, mỗi tenant 2 chi nhánh, khoảng 50 SKU, 60 ngày lịch sử bán | C, có A và B hỗ trợ | Mục f của đề | Script chạy lặp lại được |
| 9 | W5-04 | Docker Compose demo đầy đủ, Dockerfile từng service, seed tự chạy, HTTPS ở gateway, health check và restart policy | C | Mục f của đề, NFR-SEC-01, NFR-USA-01 | Máy sạch: clone, compose up, đăng nhập được |
| 9 | W5-05 | CI chạy test và build image | C | Mục e.7 của đề | Pipeline xanh trên `main` |

## Quy tắc riêng

- Mọi container có health check và restart policy (NFR-USA-01).
- Seed chạy lặp lại được mà không tạo dữ liệu trùng.
- Không đưa secret thật vào repo; cấu hình mẫu nằm trong `.env.example`.
- Kịch bản nghiệm thu: máy sạch, clone, `docker compose up`, đăng nhập được (kịch bản T20).
