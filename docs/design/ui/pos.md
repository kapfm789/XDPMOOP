# Giao diện: POS

Ứng dụng bán tại quầy cho Cashier, nằm ở `frontend/pos`. Toàn bộ việc bán diễn ra trên một màn hình; mục tiêu là quét mã rồi Enter là xong một đơn (NFR-USA-02).

## Màn hình

| Route | Màn hình | Vai trò | API | Use case | Task |
| --- | --- | --- | --- | --- | --- |
| `/login` | Đăng nhập | Công khai | `POST /api/identity/auth/login` | UC-AUTH-02 | W1-09 |
| `/` | Bán hàng | Cashier, Owner | `GET /api/core/pos/skus`, `POST /api/core/pos/checkout` | UC-POS-01, UC-POS-02 | W3-07 |
| `/receipt/:orderId` | Hóa đơn để in | Cashier, Owner | Dùng dữ liệu đơn vừa trả về | UC-POS-03 | W3-07 |

## Bố cục màn hình bán hàng

| Vùng | Nội dung | Desktop và tablet ngang | Thiết bị cầm tay |
| --- | --- | --- | --- |
| Ô tìm kiếm | Luôn giữ focus; nhận tên, mã SKU hoặc mã vạch | Trên cùng, bên trái | Trên cùng |
| Kết quả | Tối đa 20 SKU: tên, giá lẻ, tồn khả dụng | Bên trái, dưới ô tìm | Dưới ô tìm, cuộn dọc |
| Giỏ hàng | Dòng hàng, số lượng, thành tiền, tổng | Bên phải | Tab riêng, có huy hiệu số món |
| Thanh toán | Chọn tiền mặt hoặc QR, nút thanh toán | Bên phải, dưới giỏ | Thanh cố định ở đáy |

Nút và dòng hàng cao tối thiểu 44 px để chạm bằng ngón tay.

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
- Thông báo `OrderCreated` của chi nhánh hiện popup kèm âm thanh và không lấy mất focus của ô tìm.

## PWA

- `manifest.webmanifest` cho phép cài lên màn hình chính.
- Service worker chỉ cache app shell (HTML, JS, CSS, icon). Không cache phản hồi API và không xếp hàng đợi đơn khi mất mạng.
- Mất mạng thì hiện băng thông báo và khóa nút thanh toán.
