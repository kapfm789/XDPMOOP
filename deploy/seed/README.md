# Seed data

Dữ liệu demo cho máy dev và gói demo (task W4-09, [docs/plans/deploy.md](../../docs/plans/deploy.md)): 2 tenant, mỗi tenant 2 chi nhánh, khoảng 50 SKU và 60 ngày lịch sử bán. Seed chạy lặp lại được mà không tạo bản trùng.

## Chạy

Cần Compose dev đang chạy và mọi service đã khỏe:

```bash
docker compose -f deploy/docker-compose.yml up -d
docker compose -f deploy/docker-compose.yml run --rm seed
```

Lần đầu mất vài phút vì phải build trong container và chờ `insights` nhận hết lịch sử bán. Máy có .NET 8 thì chạy thẳng được từ gốc repo, với gateway ở `http://localhost:8080` và RabbitMQ ở `localhost:5672`:

```bash
dotnet run --project deploy/seed
```

| Biến | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `SEED_GATEWAY_URL` | `http://localhost:8080` | Gateway; chỉ nhận `localhost`, `127.0.0.1` hoặc service `gateway` của Compose |
| `SEED_RABBITMQ` | `amqp://oism:oism@localhost:5672` | RabbitMQ để phát lịch sử bán |
| `SEED_PASSWORD` | `Demo@2026` | Mật khẩu của mọi tài khoản do seed tạo |

## Tài khoản

| Tenant | Owner | Staff | Cashier (chi nhánh đầu) |
| --- | --- | --- | --- |
| Thời trang Mộc | `owner@moc.test` | `staff@moc.test` | `cashier@moc.test` |
| Tạp hóa Xanh | `owner@xanh.test` | `staff@xanh.test` | `cashier@xanh.test` |

Mật khẩu lấy từ `SEED_PASSWORD`. Đây là dữ liệu demo, không dùng cho môi trường thật.

## Seed tạo gì

Phần dữ liệu nền đi qua gateway API như người dùng thao tác, nên mọi quy tắc nghiệp vụ, sổ kho, outbox và event chạy đúng đường của hệ thống:

- Tenant, Owner, Staff, Cashier; hai chi nhánh. Chi nhánh đầu có tài khoản nhận chuyển khoản giả (Vietcombank, số `0000000000`) để POS hiện mã VietQR; muốn quét chuyển khoản thật thì Owner đổi sang tài khoản thật ở **Cài đặt › Chi nhánh**.
- Danh mục, thương hiệu, sản phẩm có và không có biến thể, giá lẻ và giá sỉ, mỗi SKU một mã vạch EAN-13 do `catalog` sinh.
- Một nhà cung cấp và mỗi chi nhánh một phiếu nhập đầu kỳ đã xác nhận, khoảng 18 ngày bán. Vài SKU chỉ nhập 2 cái và có ngưỡng 6 để trang tổng quan có cảnh báo tồn.
- Ngưỡng tồn cho mọi SKU, khoảng 4 ngày bán.
- Hoạt động trong ngày: 3 đơn POS mỗi chi nhánh, 2 đơn đặt qua điện thoại đang giữ hàng chờ duyệt (hết hạn giữ sau 30 phút như đơn thật), 1 phiếu chuyển kho đang vận chuyển, 1 shop Shopee để bắn tải đơn sàn.

Lịch sử 60 ngày tính tới hết hôm qua không tạo được qua API vì `core` ghi mốc xác nhận bằng giờ hiện tại. Seed phát thẳng các event `OrderConfirmed` có `confirmedAt` lùi ngày lên RabbitMQ, đúng phong bì mà outbox của `core` vẫn phát; `insights` nhận qua consumer thật vào `sales_facts`. Hệ quả:

- Báo cáo doanh thu, bán chạy, bán chậm và dự báo có đủ 60 ngày để xem.
- `core` không có các đơn lịch sử đó và tồn kho không bị trừ theo chúng: đây là lịch sử bán coi như đã nhập từ trước khi dùng hệ thống.
- Hình dạng nhu cầu đủ cho các màn hình: SKU bán đều, SKU tăng, SKU giảm, SKU mới chỉ có 5 ngày lịch sử (dự báo hiện "chưa đủ dữ liệu") và SKU không bán được gì (đứng đầu bán chậm).

Cuối cùng seed chờ `insights` nhận đủ lịch sử rồi chạy dự báo một lần, để màn hình đề xuất nhập có số ngay.

## Chạy lại

Mỗi bước tra trước rồi mới tạo: chi nhánh theo mã, người dùng theo email, sản phẩm theo tên, SKU theo mã, phiếu nhập đầu kỳ theo ghi chú, đơn POS theo `Idempotency-Key` cố định. Đơn và event lịch sử mang id dựng từ tenant, chi nhánh và ngày, nên `insights` bỏ qua bản đã nhận. Chạy lại vào ngày khác thì lịch sử có thêm những ngày mới tới hết hôm qua; ngày đã có giữ nguyên.

Ngưỡng tồn được đặt lại mỗi lần chạy, nên mỗi lần chạy phát thêm `StockChanged` với cùng giá trị ngưỡng.
