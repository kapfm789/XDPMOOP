# frontend/pos — kế hoạch folder

Ứng dụng bán tại quầy cho Cashier: tìm hoặc quét mã, giỏ hàng, thanh toán, in hóa đơn; cài được như PWA (FR-POS-01, FR-POS-03). Owner: Dev C. Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Phân lớp

```text
pos/
├─ public/         manifest.webmanifest, icon, service worker cache app shell
└─ src/
   ├─ pages/       màn hình bán hàng, màn hình in hóa đơn
   ├─ features/    tìm SKU, giỏ hàng, thanh toán, phím tắt
   └─ app/         layout cảm ứng, router
```

`pages` gọi `features`, `features` gọi `frontend/shared`.

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 5 | W3-07 | pos: tìm theo tên, SKU, mã vạch; giỏ hàng; phím tắt; tiền mặt và QR; in qua trình duyệt | C | FR-POS-01, FR-POS-03, NFR-USA-02 | Quét mã rồi Enter là xong đơn; chạy tốt trên desktop, tablet, điện thoại |
| 7 | W4-08 | admin và pos: nhận thông báo SignalR (âm thanh, popup); PWA manifest và service worker | C | FR-SIM-02, FR-POS-01 | Đơn mới hiện không cần tải lại; POS cài được lên màn hình chính |
| 10 | W5-11 | pos: giao diện cùng phong cách với admin (W5-10): theme, thanh trên có logo, chi nhánh, thu ngân và trạng thái mạng, kết quả tìm dạng ô, giỏ và thanh toán dạng thẻ, đăng nhập và hóa đơn | C | FR-POS-01, NFR-USA-02 | Màn hình POS theo mục "Bố cục và phong cách" của docs/design/ui/pos.md; phím tắt và luồng quét mã rồi Enter giữ nguyên |
| 10 | W5-12 | identity, admin, pos: tài khoản nhận chuyển khoản theo chi nhánh; POS hiện mã VietQR của chi nhánh và in phiếu thanh toán tạm tính kèm mã | C | FR-AUTH-04, FR-POS-03 | Owner khai tài khoản nhận cho chi nhánh; POS ở chi nhánh đó hiện và in được mã VietQR quét được bằng app ngân hàng; in phiếu thanh toán không tạo đơn |
| 10 | W5-13 | pos: quét mã vạch bằng camera và bằng ảnh tải lên | C | FR-POS-01, NFR-USA-02 | Quét mã EAN-13 hoặc Code128 bằng camera hay bằng ảnh thì hàng vào giỏ như máy quét; máy không có camera hoặc không chạy HTTPS vẫn quét được bằng ảnh |

## Quy tắc riêng

- Mỗi lần bấm thanh toán sinh một `Idempotency-Key` và giữ nguyên khóa đó khi thử lại (kịch bản T03).
- Kiểm tồn ở giao diện chỉ để báo sớm; `POST /pos/checkout` ở `core` mới là nơi quyết định (FR-POS-02).
- Hóa đơn chỉ in sau khi backend trả đơn đã lưu; in lỗi thì in lại, không gọi checkout lần nữa. Phiếu thanh toán tạm tính kèm mã VietQR in từ giỏ, không gọi API và không tạo đơn (W5-12).
- Hoàn tất đơn trong dưới 3 lần nhấp, hoặc quét mã rồi Enter (NFR-USA-02).
- Không bán offline (D-11); service worker chỉ cache app shell.
- Màu, bố cục và cách trình bày theo [design/ui/pos.md](../design/ui/pos.md) mục "Bố cục và phong cách" (W5-11).
