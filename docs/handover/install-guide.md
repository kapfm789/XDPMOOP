# Hướng dẫn cài đặt

Cài gói demo của OISM lên một máy chưa có gì ngoài hệ điều hành. Làm đúng các bước dưới là đăng nhập được trang quản trị và POS với dữ liệu mẫu (kịch bản T20). Người đang phát triển code dùng Compose dev thay vì gói này: [README.md](../../README.md) mục "Chạy thử trên máy".

## 1. Yêu cầu máy

| Hạng mục | Tối thiểu | Ghi chú |
| --- | --- | --- |
| Hệ điều hành | Windows 10 22H2 hoặc 11, macOS 13, Ubuntu 22.04 | Windows cần WSL 2 cho Docker Desktop |
| CPU | 4 nhân | Lần build đầu dùng nhiều CPU nhất |
| RAM | 8 GB, dành cho Docker ít nhất 4 GB | 10 container chạy thường trực, thêm 2 container chạy một lần lúc cài |
| Ổ đĩa trống | 15 GB | Image .NET SDK, Node, PostgreSQL, RabbitMQ và các image build ra |
| Mạng | Có Internet ở lần cài đầu | Tải image và package; chạy xong thì không cần |
| Cổng trống | 5173, 5174, 8080, 8443 | Trang quản trị, POS, gateway HTTP và HTTPS |

Trình duyệt: Chrome, Edge hoặc Firefox bản gần đây. POS cài thành ứng dụng (PWA) được trên Chrome và Edge.

## 2. Cài phần mềm

1. Cài [Docker Desktop](https://www.docker.com/products/docker-desktop/). Trên Windows, chọn dùng WSL 2 khi trình cài hỏi, rồi khởi động lại máy nếu được yêu cầu. Trên Linux có thể dùng Docker Engine kèm plugin Compose thay cho Docker Desktop.
2. Mở Docker Desktop và chờ tới khi biểu tượng báo "Engine running".
3. Cài [Git](https://git-scm.com/downloads). Trên Windows, bộ cài có kèm Git Bash; các lệnh dưới chạy được trong Git Bash, PowerShell hoặc Terminal.

Kiểm tra:

```bash
docker version
```

```bash
docker compose version
```

Cả hai lệnh phải in ra phiên bản, không báo lỗi. Compose phải là bản 2 trở lên (lệnh `docker compose`, không phải `docker-compose`).

Không cần cài .NET, Node.js, PostgreSQL hay RabbitMQ: mọi thứ chạy trong container.

## 3. Lấy mã nguồn

```bash
git clone https://github.com/kapfm789/XDPMOOP.git
```

```bash
cd XDPMOOP
```

Nhận mã nguồn dạng file nén thì giải nén rồi mở terminal ở thư mục vừa giải nén; các bước sau giống nhau.

## 4. Chạy gói demo

```bash
docker compose -f deploy/docker-compose.demo.yml up -d --build
```

Lần đầu mất khoảng 10 tới 15 phút. Lệnh làm lần lượt:

1. Tải image nền, build 6 image .NET (gateway và 5 service), image seed, 2 image frontend.
2. Container `secrets` sinh cặp khóa JWT và chứng chỉ TLS tự ký vào một volume riêng, chỉ ở lần chạy đầu.
3. PostgreSQL và RabbitMQ khởi động; 5 service áp migration của mình rồi báo khỏe.
4. Container `seed` nạp dữ liệu mẫu, chờ `insights` nhận đủ 60 ngày lịch sử bán, chạy dự báo một lần rồi thoát.

Xem tiến độ:

```bash
docker compose -f deploy/docker-compose.demo.yml ps -a
```

```bash
docker compose -f deploy/docker-compose.demo.yml logs -f seed
```

Cài xong khi `seed` ở trạng thái `Exited (0)` và mọi container còn lại ở trạng thái `Up ... (healthy)`. Thoát lệnh xem log bằng Ctrl+C; các container vẫn chạy.

## 5. Mở và đăng nhập

| Địa chỉ | Là gì |
| --- | --- |
| http://localhost:5173 | Trang quản trị (`admin`) cho chủ cửa hàng và nhân viên |
| http://localhost:5174 | Bán tại quầy (`pos`) cho thu ngân |
| http://localhost:8080/swagger | Tài liệu API (Swagger) của 5 service |
| https://localhost:8443 | Gateway qua HTTPS, TLS 1.3, chứng chỉ tự ký nên trình duyệt sẽ cảnh báo |

Tài khoản mẫu, mật khẩu chung `Demo@2026`:

| Cửa hàng | Chủ cửa hàng (admin, POS) | Nhân viên (admin) | Thu ngân (POS) |
| --- | --- | --- | --- |
| Thời trang Mộc | `owner@moc.test` | `staff@moc.test` | `cashier@moc.test` |
| Tạp hóa Xanh | `owner@xanh.test` | `staff@xanh.test` | `cashier@xanh.test` |

Mật khẩu đổi được bằng biến `SEED_PASSWORD` trong `deploy/.env` trước lần chạy đầu ([deploy/.env.example](../../deploy/.env.example)).

## 6. Kiểm tra cài đặt

| Kiểm | Cách làm | Kết quả đúng |
| --- | --- | --- |
| Đăng nhập | Vào http://localhost:5173 bằng `owner@moc.test` | Trang tổng quan có thẻ doanh thu, biểu đồ 7 ngày, cảnh báo tồn |
| Bán tại quầy | Vào http://localhost:5174 bằng `cashier@moc.test`, gõ `PK-TL` rồi Enter, Enter lần nữa | Hiện hóa đơn để in |
| API | Mở http://localhost:8080/swagger | Ô chọn định nghĩa có identity, catalog, core, channel, insights |
| Toàn bộ API (tùy chọn) | Chạy Postman collection theo [design/api/postman/README.md](../design/api/postman/README.md) | Mọi test xanh |

## 7. Dừng, chạy lại, xóa

| Việc | Lệnh |
| --- | --- |
| Dừng, giữ dữ liệu | `docker compose -f deploy/docker-compose.demo.yml down` |
| Chạy lại sau khi dừng | `docker compose -f deploy/docker-compose.demo.yml up -d` (không cần `--build` nếu mã nguồn không đổi) |
| Xóa sạch dữ liệu, khóa và chứng chỉ | `docker compose -f deploy/docker-compose.demo.yml down -v` |
| Nạp lại dữ liệu mẫu | `docker compose -f deploy/docker-compose.demo.yml up -d seed`; seed chạy lặp lại không tạo bản trùng |

## 8. Sự cố thường gặp

| Hiện tượng | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| `Bind for 0.0.0.0:8080 failed: port is already allocated` | Cổng đang bị chương trình khác dùng, thường là Compose dev của dự án | Dừng chương trình đó (`docker compose -f deploy/docker-compose.yml down` nếu là Compose dev) rồi chạy lại lệnh ở bước 4 |
| Lệnh `docker` báo không kết nối được Docker daemon | Docker Desktop chưa chạy xong | Mở Docker Desktop, chờ "Engine running" |
| Build dừng với lỗi tải package (`NU1301`, `npm ERR! network`) | Mạng chập chờn lúc tải package | Chạy lại lệnh ở bước 4; phần đã build được giữ trong cache |
| Build dừng với `failed to receive status: rpc error ... EOF` | Máy ảo của Docker thiếu RAM khi build 9 image song song | Build lần lượt từng image rồi chạy lại bước 4 không cần `--build`: `docker compose -f deploy/docker-compose.demo.yml build gateway`, rồi lần lượt `identity`, `catalog`, `core`, `channel`, `insights`, `seed`, `admin`, `pos` |
| `seed` khởi động lại nhiều lần rồi `Exited (1)` | Service chưa khỏe kịp, thường do máy thiếu RAM | Tăng RAM cho Docker (Docker Desktop, Settings, Resources), rồi `docker compose -f deploy/docker-compose.demo.yml up -d seed` |
| Đăng nhập báo sai mật khẩu với tài khoản mẫu | Seed chưa chạy xong, hoặc `SEED_PASSWORD` đã đổi | Xem `logs seed`; dùng mật khẩu trong `deploy/.env` nếu có |
| Trang quản trị trắng hoặc báo lỗi mạng | Gateway chưa khỏe | `docker compose -f deploy/docker-compose.demo.yml ps`, chờ `gateway` ở `healthy` |
| Trình duyệt cảnh báo chứng chỉ ở cổng 8443 | Chứng chỉ tự ký | Bình thường với gói demo; hai frontend dùng cổng HTTP 8080 nên không bị ảnh hưởng |

Cần xem log một service: `docker compose -f deploy/docker-compose.demo.yml logs core` (thay `core` bằng service khác).
