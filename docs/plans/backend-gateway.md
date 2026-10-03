# backend/gateway — kế hoạch folder

Cổng vào duy nhất của frontend: định tuyến tới 5 service, kiểm JWT, kết thúc TLS, chuyển tiếp WebSocket cho SignalR. Owner: Dev A. Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Thuộc tầng nào

Tầng cổng vào (mục 4.1 của kế hoạch tổng). Không có database, không có nghiệp vụ.

## Cấu trúc

```text
gateway/
└─ Oism.Gateway/
   ├─ Program.cs          YARP, xác thực JWT, CORS, rate limit
   ├─ appsettings.json    route /api/identity, /api/catalog, /api/core, /api/channel, /api/insights, /hubs
   └─ Swagger/            gộp Swagger của 5 service
```

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 1 | W1-03 | Docker Compose dev: PostgreSQL một database với 5 schema, RabbitMQ, gateway YARP | A | NFR-SEC-01 | `docker compose up` chạy; health check 5 service qua gateway |
| 9 | W5-04 | Docker Compose demo đầy đủ, Dockerfile từng service, seed tự chạy, HTTPS ở gateway, health check và restart policy | C | Mục f của đề, NFR-SEC-01, NFR-USA-01 | Máy sạch: clone, compose up, đăng nhập được |
| 10 | W5-06 | Swagger từng service gộp ở gateway, Postman collection | C | Mục f của đề | Gọi theo tài liệu ra đúng kết quả |

## Quy tắc riêng

- Route không yêu cầu JWT đúng bằng danh sách endpoint công khai ở [security.md](../architecture/security.md): đăng ký tenant, đăng nhập, làm mới token, `POST /api/channel/webhooks/{channel}` và `/api/<service>/health`. Mọi route khác yêu cầu.
- Gateway chỉ chuyển tiếp: không sửa nội dung request, chỉ truyền header xác thực và correlation id.
- HTTPS với TLS 1.3 bật ở gateway (NFR-SEC-01); các service phía sau nói HTTP trong mạng nội bộ của Compose.
