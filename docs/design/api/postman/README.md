# Postman collection

[oism.postman_collection.json](oism.postman_collection.json) gọi lần lượt API của 5 service qua gateway, đúng đường dẫn, vai trò, dữ liệu vào ra và mã lỗi ghi ở [design/api/](../README.md). Mỗi request có test kiểm mã trạng thái và hình dạng phản hồi; cả collection là kịch bản T21 ở [testing/scenarios.md](../../../testing/scenarios.md).

## Chạy

Cần một gói Compose đang chạy (dev hoặc demo, [deploy/README.md](../../../../deploy/README.md)). Collection tự đăng ký một tenant mới mỗi lần chạy, nên chạy lặp lại được và không đụng dữ liệu seed.

Bằng Postman: Import file, mở collection, bấm Run, giữ nguyên thứ tự request. Bằng dòng lệnh, chỉ cần Docker:

```bash
docker run --rm -v "$PWD/docs/design/api/postman:/etc/newman" postman/newman:6-alpine run oism.postman_collection.json --env-var baseUrl=http://host.docker.internal:8080
```

Trên Linux không có Docker Desktop, thêm `--add-host host.docker.internal:host-gateway` ngay sau `--rm`.

| Biến | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `baseUrl` | `http://localhost:8080` | Gateway; newman chạy trong container thì dùng `http://host.docker.internal:8080` |
| `password` | `Postman@2026` | Mật khẩu của các tài khoản collection tự tạo |

Các biến còn lại (`accessToken`, `branchId`, `skuAId` ...) do test script của request trước ghi vào biến collection.

## Nội dung

| Thư mục | Use case | Điểm kiểm chính |
| --- | --- | --- |
| 0. Hệ thống | | Health check công khai; Swagger gộp ở gateway có `servers` trỏ về `/api/core` |
| 1. identity | UC-AUTH-01 đến 04 | Đăng ký, trùng email 409, đăng nhập sai 401, refresh xoay vòng, chi nhánh, người dùng, Cashier gọi API quản trị nhận 403 |
| 2. catalog | UC-PROD-01 đến 04 | Cây danh mục, xóa danh mục còn con 409, sản phẩm hai biến thể, trùng mã SKU 409, EAN-13 tự sinh và sai số kiểm tra 400, giá chỉ Owner đặt |
| 3. core | UC-INV-01 đến 05, UC-ORD-02 đến 06, UC-POS-01, 02 | Giá vốn 110.000 sau hai phiếu nhập (T09), xác nhận lại phiếu (T11), Staff không thấy `avgCost`, sổ chỉ đọc, duyệt chốt `costPrice`, hủy sau duyệt 409 (T08), hủy hai lần (T06), vượt tồn 409 kèm `details`, POS bấm hai lần một đơn (T03), chuyển kho chưa nhận (T12), kiểm kê |
| 4. channel | UC-SIM-01 | `secret` chỉ trả lúc tạo shop, chữ ký sai 401, webhook có chữ ký HMAC, gửi lại không tạo đơn thứ hai (T03), đơn sàn tới Reserved rồi được duyệt, bắn tải |
| 5. insights | UC-REP-01 đến 03, UC-AI-01, 02 | Lợi nhuận gộp ví dụ 300.000 − 220.000 = 80.000 cộng thêm đơn POS và đơn sàn (T10), Staff xem lợi nhuận 403, cảnh báo tồn, giá trị tồn theo giá vốn, bán chạy, bán chậm, SKU mới "chưa đủ dữ liệu" (T19) |
| 6. Kết thúc phiên | UC-AUTH-02 | Dùng lại refresh token đã xoay 401, đăng xuất thu hồi token |

Dữ liệu đi giữa các service bằng event nên có độ trễ ngắn. Các request đứng đầu một chặng phụ thuộc event (`Chờ core nhận SKU và chi nhánh`, `Chờ đơn sàn được giữ hàng`, các báo cáo của `insights`) tự gọi lại tối đa 20 lần, cách nhau 1 giây, rồi mới báo lỗi.

## Sửa collection

Thêm hoặc đổi endpoint thì sửa [design/api/](../README.md) trước, rồi thêm request vào đúng thư mục, kèm test kiểm mã trạng thái và các trường chính. Export lại từ Postman theo định dạng Collection v2.1 và ghi đè file này.
