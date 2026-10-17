# Triển khai

Hai gói Compose, cùng dùng một PostgreSQL với 5 schema và một RabbitMQ ([docs/plans/deploy.md](../docs/plans/deploy.md)):

| File | Dùng khi | Cách chạy service |
| --- | --- | --- |
| `docker-compose.yml` | Đang code | Image SDK, mount mã nguồn `backend/`, build lại mỗi lần khởi động; frontend chạy bằng `npm run dev` trên máy |
| `docker-compose.demo.yml` | Trình diễn, chấm bài, máy sạch | Build image thật từ Dockerfile của từng phần; `admin` và `pos` chạy bằng nginx; seed tự chạy |

Hai gói dùng cùng cổng nên chỉ bật một gói một lúc.

## Gói demo

Máy chỉ cần Docker (Docker Desktop trên Windows hoặc macOS). Từ gốc repo vừa clone:

```bash
docker compose -f deploy/docker-compose.demo.yml up -d --build
```

Lần đầu mất khoảng 10 tới 15 phút: tải image, build 6 image .NET, 2 image frontend và seed. Lúc khởi động, `secrets` sinh cặp khóa JWT và chứng chỉ TLS tự ký vào volume riêng; các service áp migration rồi báo khỏe; sau đó `seed` nạp dữ liệu demo, chờ `insights` nhận đủ lịch sử bán, chạy dự báo một lần rồi thoát. Xem tiến độ:

```bash
docker compose -f deploy/docker-compose.demo.yml ps
docker compose -f deploy/docker-compose.demo.yml logs -f seed
```

Khi `seed` ở trạng thái `exited (0)`:

| Địa chỉ | Là gì |
| --- | --- |
| `http://localhost:5173` | Trang quản trị (`admin`) |
| `http://localhost:5174` | Bán hàng tại quầy (`pos`), cài được thành PWA |
| `http://localhost:8080` | Gateway, HTTP; hai frontend gọi cổng này |
| `https://localhost:8443` | Gateway, HTTPS với TLS 1.3; chứng chỉ tự ký nên trình duyệt sẽ cảnh báo |

Tài khoản demo và mật khẩu ở [seed/README.md](seed/README.md); mật khẩu đổi được bằng biến `SEED_PASSWORD` trong `deploy/.env` trước lần `up` đầu.

Chỉ gateway, `admin` và `pos` mở cổng ra máy host. PostgreSQL, RabbitMQ và 5 service chỉ nằm trong mạng của Compose; cần vào database thì dùng `docker compose -f deploy/docker-compose.demo.yml exec postgres psql -U oism -d oism`.

Kiểm HTTPS chỉ nhận TLS 1.3 bằng `openssl` (Git Bash trên Windows có sẵn):

```bash
openssl s_client -connect localhost:8443 -tls1_3 </dev/null
openssl s_client -connect localhost:8443 -tls1_2 </dev/null
```

Lệnh đầu in `Protocol: TLSv1.3` kèm cảnh báo chứng chỉ tự ký; lệnh sau báo `alert protocol version` vì gateway không nhận TLS 1.2. Đừng kiểm bằng `curl` của Windows 10: nó dùng Schannel, vốn chưa có TLS 1.3, nên lỗi cả khi gateway đúng. Trình duyệt Chrome, Edge, Firefox đều mở được `https://localhost:8443/health` sau khi chấp nhận chứng chỉ.

Dừng mà giữ dữ liệu: `docker compose -f deploy/docker-compose.demo.yml down`. Xóa sạch cả dữ liệu, khóa và chứng chỉ: thêm `-v`.

## Mọi container có health check và restart policy

| Container | Health check | Restart |
| --- | --- | --- |
| `postgres`, `rabbitmq` | `pg_isready`, `rabbitmq-diagnostics ping` | `unless-stopped` |
| `gateway` và 5 service | `GET /health` | `unless-stopped` |
| `admin`, `pos` | `GET /` của nginx | `unless-stopped` |
| `secrets` | Không: chạy một lần rồi thoát | Không; service khác chờ nó thoát thành công |
| `seed` | Không: chạy một lần rồi thoát | `on-failure:3` |

Kịch bản kiểm khởi động lại (NFR-USA-01): cho tiến trình của một service tự thoát như khi nó sập, rồi xem Docker bật lại và health check trở về `healthy`:

```bash
docker exec oism-demo-core-1 sh -c 'kill 1'
docker inspect -f '{{.RestartCount}} {{.State.Health.Status}}' oism-demo-core-1
```

`docker stop` hay `docker kill` thì Docker coi là dừng bằng tay nên không bật lại; đó là cách `unless-stopped` vận hành.
