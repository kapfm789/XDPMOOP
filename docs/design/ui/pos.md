# Giao diện: POS

Ứng dụng bán tại quầy cho Cashier, nằm ở `frontend/pos`. Toàn bộ việc bán diễn ra trên một màn hình; mục tiêu là quét mã rồi Enter là xong một đơn (NFR-USA-02).

## Màn hình

| Route | Màn hình | Vai trò | API | Use case | Task |
| --- | --- | --- | --- | --- | --- |
| `/login` | Đăng nhập | Công khai | `POST /api/identity/auth/login` | UC-AUTH-02 | W1-09 |
| `/` | Bán hàng | Cashier, Owner | `GET /api/core/pos/skus`, `POST /api/core/pos/checkout`; thanh trên đọc tên cửa hàng ở `GET /api/identity/me` | UC-POS-01, UC-POS-02 | W3-07, W5-11 |
| `/receipt/:orderId` | Hóa đơn để in | Cashier, Owner | Dùng dữ liệu đơn vừa trả về | UC-POS-03 | W3-07 |

## Bố cục màn hình bán hàng

| Vùng | Nội dung | Desktop và tablet ngang | Thiết bị cầm tay |
| --- | --- | --- | --- |
| Ô tìm kiếm | Luôn giữ focus; nhận tên, mã SKU hoặc mã vạch | Trên cùng, bên trái | Trên cùng |
| Kết quả | Tối đa 20 SKU: tên, giá lẻ, tồn khả dụng | Bên trái, dưới ô tìm | Dưới ô tìm, cuộn dọc |
| Giỏ hàng | Dòng hàng, số lượng, thành tiền, tổng | Bên phải | Tab riêng, có huy hiệu số món |
| Thanh toán | Chọn tiền mặt hoặc QR, nút thanh toán | Bên phải, dưới giỏ | Thanh cố định ở đáy |

Nút và dòng hàng cao tối thiểu 44 px để chạm bằng ngón tay.

## Bố cục và phong cách

POS dùng cùng phong cách với `admin` (task W5-11, theo [admin.md](admin.md) mục "Bố cục và phong cách"): cùng bảng màu, bo góc và font Inter, đặt bằng theme token của Ant Design; không thêm package giao diện hay icon. Không có bản Figma riêng cho POS; các vùng và quy tắc của màn hình bán hàng vẫn theo hai mục trên.

| Hạng mục | Thiết kế |
| --- | --- |
| Màu và chữ | Như `admin`: màu chính `#1677ff`, nền trang `#f5f7fa`, thẻ trắng, viền `#e4e9f0`, chữ `#172033`, chữ phụ `#697386`. Xanh lá là còn hàng, cam là sắp chạm tồn khả dụng, đỏ là hết hàng hoặc lỗi. Số tiền to, in đậm |
| Thanh trên | Nền trắng, viền dưới; logo OISM kèm chữ "POS"; tên cửa hàng lấy từ `tenantName` của `GET /api/identity/me`; Owner chọn chi nhánh bán hàng ngay tại đây (Cashier bán ở chi nhánh trong token); chấm trạng thái mạng (Trực tuyến, Mất mạng); avatar chữ cái đầu, họ tên và tag vai trò; nút đăng xuất |
| Ô tìm | Cao, có icon kính lúp, nằm trong thẻ trên cùng của cột trái |
| Kết quả tìm | Lưới ô sản phẩm: tên, mã SKU, giá lẻ, tag tồn khả dụng; ô hết hàng mờ và không bấm được. Desktop 3 cột, tablet 2 cột, thiết bị cầm tay 1 cột |
| Giỏ hàng | Thẻ "Giỏ hàng" kèm số món; mỗi dòng có tên, mã SKU, đơn giá, nút giảm, số lượng, nút tăng, nút xóa và thành tiền; dòng đang chọn nền xanh nhạt; cuối thẻ là tổng tiền |
| Thanh toán | Thẻ riêng: chọn Tiền mặt hoặc Chuyển khoản QR, mã QR kèm số tiền khi chọn QR, nút thanh toán màu chính cao 56 px ghi số tiền, dòng gợi ý phím tắt |
| Đăng nhập | Thẻ ở giữa trang trên nền gradient nhạt, logo ở trên, như `admin` |
| Hóa đơn | Thẻ hóa đơn khổ hẹp ở giữa màn hình; bản in chỉ có hóa đơn, không có thanh trên và nút |

## Phím tắt

| Phím | Tác dụng |
| --- | --- |
| Enter trong ô tìm, có chữ | Nếu khớp đúng một mã vạch hoặc mã SKU thì thêm vào giỏ; nếu không thì tìm |
| Enter trong ô tìm, ô trống, giỏ có hàng | Thanh toán bằng phương thức đang chọn |
| F2 | Đưa focus về ô tìm |
| F4 | Đổi giữa tiền mặt và QR |
| `+`, `-` | Tăng, giảm số lượng dòng đang chọn |
| Delete | Xóa dòng đang chọn |
| Esc | Xóa nội dung ô tìm; bấm lần nữa để xóa giỏ sau khi xác nhận |

Máy quét mã vạch hoạt động như bàn phím: gõ mã rồi gửi Enter. Vì thế "quét, rồi Enter" là hai lần Enter: lần đầu thêm hàng, lần sau thanh toán.

`+`, `-` và Delete chỉ tác động lên giỏ khi ô tìm đang trống, vì mã SKU có thể chứa chính các ký tự đó. Dòng đang chọn là dòng vừa thêm hoặc vừa chạm.

## Quy tắc

- Mỗi lần bấm thanh toán sinh một `Idempotency-Key`. Nút bị khóa trong lúc chờ; lỗi mạng thì thử lại với cùng khóa.
- Tồn khả dụng hiển thị chỉ để báo sớm. Khi backend trả 409 `insufficient_stock`, giỏ cập nhật số còn lại của SKU đó và thu ngân quyết định tiếp.
- Với QR, màn hình hiện mã chuyển khoản của cửa hàng; thu ngân tự kiểm đã nhận tiền rồi mới bấm thanh toán ([ADR-0007](../../decisions/0007-pos-payment-no-offline.md)). Nội dung mã là chuỗi cấu hình ở biến `VITE_POS_QR` lúc build kèm số tiền; hệ thống không kết nối ngân hàng.
- Sau khi backend trả đơn đã lưu, ứng dụng mở `/receipt/:orderId` và gọi hộp thoại in. In lỗi thì bấm in lại; không gọi thanh toán lần nữa. Đóng hộp thoại in xong, nút "Đơn mới" đang giữ focus nên Enter đưa thu ngân về màn hình bán hàng.
- `branchId` lấy từ token của Cashier. Owner dùng POS phải chọn chi nhánh trước khi bán; lựa chọn được nhớ trên trình duyệt đó, và đổi chi nhánh thì bắt đầu giỏ mới.
- Trên thiết bị cầm tay (hẹp hơn 768 px) ô tìm không tự giành lại focus sau mỗi lần chạm, để bàn phím ảo không bật lên liên tục; từ tablet trở lên ô tìm luôn giữ focus để quét liên tiếp.
- Thông báo `OrderCreated` của chi nhánh hiện popup kèm âm thanh ở góc trên bên phải, dưới thanh tiêu đề, và không lấy mất focus của ô tìm. Cashier chỉ nhận đơn của chi nhánh mình từ server; Owner nhận của mọi chi nhánh nên POS chỉ hiện đơn của chi nhánh đang chọn.

## PWA

- `manifest.webmanifest` cho phép cài lên màn hình chính; icon 192 và 512 px nằm trong `frontend/pos/public`.
- Service worker (`public/sw.js`) chỉ cache app shell (HTML, JS, CSS, icon). Không cache phản hồi API và không xếp hàng đợi đơn khi mất mạng. HTML lấy từ mạng trước để nhận bản build mới, mất mạng mới dùng bản cache; JS và CSS mang hash nên lấy từ cache trước.
- Service worker chỉ đăng ký ở bản build; thử bằng `npm --prefix frontend run preview:pos`.
- Mất mạng thì hiện băng thông báo và khóa nút thanh toán, kể cả phím Enter; giỏ hàng giữ nguyên.
