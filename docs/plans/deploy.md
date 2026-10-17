# deploy — kế hoạch folder

Docker Compose, khởi tạo database, seed data và CI cho gói demo (mục f của đề). Owner: Dev C; Dev A dựng Compose cho máy dev ở phase 1. Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Thuộc tầng nào

Tầng dữ liệu và hạ tầng chạy (mục 4.1 của kế hoạch tổng): PostgreSQL, RabbitMQ và cách các container nối với nhau.

## Cấu trúc

```text
deploy/
├─ docker-compose.yml             Compose dev: gateway, 5 service, PostgreSQL, RabbitMQ; frontend chạy npm trên máy
├─ docker-compose.demo.yml        gói demo (W5-04): image thật, admin và pos bằng nginx, khóa và TLS tự sinh, seed tự chạy
├─ README.md                      cách chạy hai gói
├─ postgres/init-schemas.sql      tạo 5 schema trong database oism: identity, catalog, core, channel, insights
└─ seed/                          2 tenant, mỗi tenant 2 chi nhánh, khoảng 50 SKU, 60 ngày lịch sử bán; Dockerfile cho gói demo

frontend/Dockerfile, nginx.conf   image của admin và pos cho gói demo

.github/workflows/ci.yml          build, test, build image
```

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 1 | W1-03 | Docker Compose dev: PostgreSQL một database với 5 schema, RabbitMQ, gateway YARP | A | NFR-SEC-01 | `docker compose up` chạy; health check 5 service qua gateway |
| 1 | W1-11 | CI: GitHub Actions build và test backend, build frontend | C | Mục e.7 của đề | PR hiện check xanh hoặc đỏ |
| 8 | W4-09 | Seed data: 2 tenant, mỗi tenant 2 chi nhánh, khoảng 50 SKU, 60 ngày lịch sử bán | C, có A và B hỗ trợ | Mục f của đề | Script chạy lặp lại được |
| 9 | W5-04 | Docker Compose demo đầy đủ, Dockerfile từng service, seed tự chạy, HTTPS ở gateway, health check và restart policy | C | Mục f của đề, NFR-SEC-01, NFR-USA-01 | Máy sạch: clone, compose up, đăng nhập được |
| 9 | W5-05 | CI chạy test và build image | C | Mục e.7 của đề | Pipeline xanh trên `main` |

## Quy tắc riêng

- Compose dev (W1-03) không build image: gateway và 5 service chạy image SDK, mount `backend/`, mỗi lần container khởi động là `dotnet build` rồi chạy. Sửa code hay thêm migration thì khởi động lại service; `docker compose watch` làm việc đó mỗi lần lưu file, theo cấu hình `develop.watch` của từng service. Không chạy `dotnet watch` trong container: thư mục mount từ Windows không phát inotify nên nó phải quét file liên tục, 6 container cùng quét ngốn khoảng 2 nhân CPU cả lúc rảnh. `Dockerfile` của từng service để dành cho gói demo (W5-04).
- Mọi container có health check và restart policy (NFR-USA-01). Ngoại lệ là hai container chạy một lần rồi thoát của gói demo, `secrets` và `seed`; bảng đầy đủ ở [deploy/README.md](../../deploy/README.md).
- Gói demo (W5-04) là file Compose riêng, `deploy/docker-compose.demo.yml`, để Compose dev giữ nguyên cách mount mã nguồn. Service `secrets` sinh cặp khóa JWT và chứng chỉ TLS tự ký vào một volume ở lần chạy đầu. Gateway mở HTTP 8080 cho hai frontend và HTTPS 8443 chỉ nhận TLS 1.3; frontend gọi cổng HTTP để máy sạch đăng nhập được ngay mà không phải chấp nhận chứng chỉ tự ký. Các service chạy ở môi trường Development, vì đó là nơi chúng áp migration lúc khởi động. Chỉ gateway, `admin`, `pos` mở cổng ra máy host.
- Seed chạy lặp lại được mà không tạo dữ liệu trùng.
- Seed là công cụ .NET ở `deploy/seed`, chạy bằng `docker compose -f deploy/docker-compose.yml run --rm seed`. Dữ liệu nền (tenant, người dùng, chi nhánh, catalog, nhập hàng, ngưỡng, đơn trong ngày) đi qua gateway API. 60 ngày lịch sử bán không tạo được qua API vì `core` ghi mốc xác nhận bằng giờ hiện tại, nên seed phát thẳng `OrderConfirmed` lùi ngày lên RabbitMQ cho `insights`; `core` không giữ các đơn lịch sử đó và tồn kho không bị trừ theo chúng. Chi tiết và tài khoản demo: [deploy/seed/README.md](../../deploy/seed/README.md).
- Không đưa secret thật vào repo; cấu hình mẫu nằm trong `.env.example`.
- Kịch bản nghiệm thu: máy sạch, clone, `docker compose up`, đăng nhập được (kịch bản T20).
