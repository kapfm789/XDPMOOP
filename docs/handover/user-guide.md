# Hướng dẫn sử dụng

Cách dùng OISM theo từng vai trò. Đường dẫn menu ghi theo thanh bên của trang quản trị, ví dụ **Kho › Phiếu nhập**. Muốn có dữ liệu để thử, cài gói demo theo [install-guide.md](install-guide.md) và dùng tài khoản mẫu ở đó.

## Vai trò

| Vai trò | Ứng dụng | Làm được gì |
| --- | --- | --- |
| Chủ cửa hàng (Owner) | Quản trị, POS | Mọi việc trong cửa hàng của mình: chi nhánh, người dùng, giá, báo cáo lợi nhuận, dự báo, kênh sàn |
| Nhân viên (Staff) | Quản trị | Sản phẩm, kho, đơn hàng, báo cáo tồn; không đặt giá, không xem giá vốn và lợi nhuận |
| Thu ngân (Cashier) | POS | Bán tại quầy ở chi nhánh được gắn |

Menu chỉ hiện mục mà vai trò được vào. Bảng quyền đầy đủ: [architecture/security.md](../architecture/security.md).

## Bắt đầu

### Đăng ký cửa hàng mới

1. Mở trang quản trị, bấm **Đăng ký**.
2. Nhập tên cửa hàng, họ tên chủ, email hoặc số điện thoại, mật khẩu ít nhất 8 ký tự.
3. Bấm đăng ký; hệ thống tạo cửa hàng, tài khoản chủ cửa hàng và đăng nhập luôn.

### Đăng nhập, đăng xuất

- Đăng nhập bằng email hoặc số điện thoại cùng mật khẩu. Phiên tự gia hạn khi đang dùng; bỏ trống 7 ngày thì phải đăng nhập lại.
- Đăng xuất bằng biểu tượng cạnh tên ở góc trên bên phải (rê chuột vào sẽ hiện chữ "Đăng xuất").

### Bố cục trang quản trị

| Vùng | Có gì |
| --- | --- |
| Thanh bên | Menu theo nhóm: Tổng quan, Sản phẩm, Kho, Đơn hàng, Báo cáo, Kênh sàn, Cài đặt. Nút ở đầu thanh trên thu gọn thanh bên còn icon; màn hình hẹp hơn 1180 px thì tự thu gọn |
| Thanh trên | Tên cửa hàng, chuông thông báo, tên và vai trò người đang đăng nhập, nút đăng xuất |
| Đầu trang | Đường dẫn (breadcrumb) và tiêu đề trang, các nút hành động |
| Nội dung | Hàng bộ lọc, rồi các thẻ nội dung; danh sách dài có phân trang ở chân thẻ, riêng hai thẻ ở trang Tổng quan cuộn bên trong thẻ |

Màu trạng thái dùng chung: xanh lá là xong hoặc đủ hàng, cam là đang chờ hoặc sắp hết, đỏ là lỗi, đã hủy hoặc hết hàng.

## Chủ cửa hàng

### Thiết lập ban đầu

1. **Cài đặt › Chi nhánh**: thêm cửa hàng và kho với mã chi nhánh (không đổi được sau khi tạo), tên, loại, địa chỉ. Cửa hàng nhận chuyển khoản thì chọn ngân hàng và nhập số tài khoản nhận; POS ở chi nhánh đó hiện mã VietQR theo tài khoản này. Tắt chi nhánh thì không bán và không nhập hàng ở đó được nữa.
2. **Cài đặt › Người dùng**: thêm nhân viên và thu ngân. Thu ngân bắt buộc gắn một chi nhánh. Khóa tài khoản bằng cách tắt "Hoạt động".
3. **Sản phẩm › Danh mục** và **Sản phẩm › Thương hiệu**: dựng cây danh mục nhiều cấp và danh sách thương hiệu.

### Sản phẩm, mã vạch, giá

1. **Sản phẩm › Sản phẩm**, bấm **Thêm sản phẩm**. Chọn có biến thể hay không; sản phẩm có biến thể nhập từng biến thể với mã SKU và thuộc tính (ví dụ "Kích cỡ: M").
2. Mở sản phẩm vừa tạo. Ở thẻ **SKU, mã vạch và giá**:
   - **Tạo mã vạch**: hiện ở SKU chưa có mã; bấm một lần là hệ thống tự sinh mã EAN-13 hợp lệ.
   - **Thêm mã vạch**: chọn EAN-13 và để trống mã thì hệ thống tự sinh mã hợp lệ; Code128 thì nhập mã.
   - Mỗi mã vạch có hình ngay trong cột **Mã vạch**; bấm **Tải PNG** để lưu hình về máy rồi in dán lên hàng. Hình này quét được ở POS.
   - **Đặt giá**: giá lẻ và giá sỉ. Chỉ chủ cửa hàng thấy nút này.
3. Mã SKU, mã vạch trùng trong cửa hàng bị từ chối và báo ngay cạnh ô nhập.
4. Ở danh sách sản phẩm, ô tìm nhận cả mã vạch. Bấm **Tìm bằng ảnh mã vạch** và chọn ảnh chụp mã vạch: đọc được mã thì trang của sản phẩm đó mở ra ngay.

### Báo cáo và dự báo

- **Báo cáo › Lợi nhuận gộp**: doanh thu thuần, giá vốn, lợi nhuận gộp, số lượng bán; lọc theo khoảng ngày, chi nhánh, kênh, SKU và nhóm theo ngày, chi nhánh, kênh hoặc SKU. Doanh thu ghi nhận lúc đơn được duyệt (POS là lúc thanh toán).
- **Báo cáo › Tồn kho và tốc độ bán**: tab giá trị tồn theo giá vốn, bán chạy, bán chậm trong 7, 30 hoặc 90 ngày.
- **Báo cáo › Dự báo nhập hàng**: bấm **Chạy dự báo** để dự báo nhu cầu 14 ngày và số nên nhập cho mỗi SKU tại mỗi chi nhánh. Hệ thống còn chạy tự động mỗi đêm. SKU ít lịch sử hiện "Chưa đủ dữ liệu". Đề xuất chỉ để tham khảo; nhập hàng vẫn làm bằng phiếu nhập.

### Kênh sàn

**Kênh sàn** quản lý shop Shopee, TikTok, Lazada của cửa hàng:

1. **Thêm shop**: chọn kênh, nhập mã shop trên sàn và chi nhánh xuất hàng. Khóa ký webhook chỉ hiện một lần; lưu lại nếu có hệ thống ngoài gửi webhook.
2. **Bắn đơn giả lập**: gửi một loạt đơn giả vào chính hệ thống để thử; nhập mã SKU, đơn giá (bỏ trống thì đơn có giá 0), số lượng mỗi đơn, số đơn và số đơn gửi cùng lúc. Đơn hợp lệ vào danh sách đơn ở trạng thái Chờ duyệt.
3. Thẻ **Webhook đã nhận** cho biết mỗi đơn sàn đã được giữ hàng hay bị từ chối, kèm lý do (ví dụ không đủ tồn).

OISM chưa kết nối API thật của sàn; phần này là giả lập để thử luồng đơn đa kênh ([ADR-0006](../decisions/0006-channel-simulator-and-idempotency.md)).

## Chủ cửa hàng và nhân viên

### Tổng quan

Trang đầu sau khi đăng nhập có số đơn chờ duyệt, số SKU tồn thấp, danh sách đơn chờ duyệt và cảnh báo tồn thấp; hai danh sách cao bằng nhau, dài thì cuộn bên trong. Chủ cửa hàng thấy thêm doanh thu hôm nay và biểu đồ doanh thu 7 ngày nằm bên trái hai danh sách. Danh sách tự cập nhật khi có đơn mới hay tồn thấp.

### Nhập hàng

1. **Kho › Phiếu nhập**, bấm **Tạo phiếu nhập**. Chọn chi nhánh nhận, nhà cung cấp (bấm **Thêm mới** nếu chưa có), rồi từng dòng SKU, số lượng, đơn giá nhập.
2. Phiếu mới ở trạng thái Nháp, sửa được thoải mái; tồn chưa đổi.
3. Bấm **Xác nhận nhập kho**: tồn tăng, giá vốn bình quân tính lại, sổ giao dịch thêm một dòng nhập cho mỗi SKU. Phiếu đã xác nhận không sửa được.

### Xem tồn và sổ giao dịch

- **Kho › Tồn kho**: tồn thực tế, đang giữ cho đơn, khả dụng theo chi nhánh và SKU; cột Mức tồn báo Đủ hàng, Sắp hết, Hết hàng. Dải trên bảng có tổng giá trị tồn (chủ cửa hàng), số dòng tồn và số SKU tồn thấp.
- **Đặt ngưỡng** trên từng dòng: tồn khả dụng chạm ngưỡng thì có cảnh báo và thông báo. Để trống là bỏ ngưỡng.
- **Xem sổ** mở **Kho › Sổ giao dịch** của đúng SKU tại chi nhánh đó, dòng mới nhất nằm trên cùng. Sổ chỉ để xem: mọi biến động tồn đều có một dòng, không ai sửa hay xóa được.

### Chuyển kho

1. **Kho › Chuyển kho**, bấm **Tạo phiếu chuyển**: chọn nơi gửi, nơi nhận và hàng.
2. **Xuất kho**: tồn nơi gửi giảm ngay. Hàng đang được giữ cho đơn không chuyển đi được.
3. Khi hàng tới, bấm **Nhận hàng** ở nơi nhận: tồn nơi nhận mới tăng. Trong lúc vận chuyển, nơi nhận chưa bán được số hàng đó.
4. Phiếu còn Nháp thì xóa được.

### Kiểm kê

1. **Kho › Kiểm kê**, bấm **Mở phiên kiểm kê** cho một chi nhánh.
2. Nhập số đếm thực tế của các SKU; có thể nạp sẵn mọi SKU của chi nhánh.
3. **Chốt**: hệ thống ghi bút toán điều chỉnh cho SKU có chênh lệch. Nếu số đếm thấp hơn lượng đang giữ cho đơn, việc chốt bị chặn và màn hình liệt kê các đơn đang giữ để xử lý trước.

### Đơn hàng

- **Đơn hàng › Danh sách đơn**: đơn của mọi kênh (POS, thủ công, Shopee, TikTok, Lazada). Tab trạng thái kèm số đơn; lọc thêm theo kênh, chi nhánh, ngày tạo.
- **Đơn hàng › Tạo đơn**: đơn khách đặt qua điện thoại hay tin nhắn. Tạo xong là hàng được giữ 30 phút.
- Mở một đơn để xem thanh bước Tạo, Giữ hàng, Đã duyệt, Hoàn tất, các dòng hàng, phần giữ hàng và thanh toán (đơn POS):
  - **Duyệt đơn**: xuất kho, chốt giá vốn. Đơn đã duyệt không hủy được.
  - **Hủy đơn**: chỉ khi còn Chờ duyệt; hàng đang giữ quay lại bán được.
  - **Hoàn tất**: sau khi giao xong.
  - **In phiếu giao**: in mã đơn, kênh, chi nhánh và danh sách hàng.
- Đơn chờ duyệt quá 30 phút tự hủy và trả hàng về tồn.

## Thu ngân: bán tại quầy

1. Mở POS, đăng nhập. Thu ngân bán ở chi nhánh được gắn; chủ cửa hàng chọn chi nhánh trước khi bán.
2. Quét mã vạch hoặc gõ tên, mã SKU vào ô tìm rồi Enter. Mã khớp đúng thì hàng vào giỏ ngay; không thì hiện danh sách để chọn. Không có máy quét thì bấm nút camera cạnh ô tìm: đưa mã vạch vào khung camera để quét liên tiếp, hoặc bấm **Chọn ảnh** để đọc mã từ ảnh chụp sẵn. Camera chỉ mở được khi POS chạy trên HTTPS hoặc `localhost` và trình duyệt được cấp quyền. Muốn dùng điện thoại làm máy quét cho máy tính: đăng nhập cùng tài khoản trên cả hai máy, trên điện thoại mở hộp quét và bật **Gửi sang máy tính**; mỗi mã quét được sẽ vào giỏ của màn hình bán hàng trên máy tính.
3. Chỉnh số lượng bằng phím `+`, `-`, xóa dòng bằng Delete (khi ô tìm trống), hoặc chạm vào nút trên dòng.
4. Chọn tiền mặt hoặc QR (phím F4). Với QR, đưa khách quét mã trên màn hình, hoặc bấm **In phiếu thanh toán** để in phiếu tạm tính kèm mã cho khách quét; phiếu không tạo đơn. Kiểm đã nhận tiền trong tài khoản trước khi xác nhận.
5. Enter khi ô tìm trống, hoặc bấm **Thanh toán** (với QR là **Xác nhận đã nhận tiền**). Đơn lưu xong thì hóa đơn mở ra và hộp thoại in bật lên; Enter tiếp để sang đơn mới.

| Phím | Tác dụng |
| --- | --- |
| Enter | Thêm hàng khớp mã; ô trống thì thanh toán |
| F2 | Về ô tìm |
| F4 | Đổi tiền mặt, QR |
| `+`, `-`, Delete | Tăng, giảm, xóa dòng đang chọn |
| Esc | Xóa ô tìm; bấm lần nữa để xóa giỏ |

- Bấm thanh toán hai lần, hay mạng chập chờn rồi thử lại, vẫn chỉ ra một đơn.
- Mất mạng thì POS báo trên đầu màn hình và khóa nút thanh toán; giỏ hàng giữ nguyên. POS không bán ngoại tuyến.
- Trên Chrome hoặc Edge, cài POS lên màn hình chính bằng biểu tượng cài đặt ở thanh địa chỉ.

## Thông báo

- Đơn mới (mọi kênh) và tồn thấp hiện popup kèm âm thanh ở góc màn hình, giữ 10 giây; bấm vào để mở đơn hoặc màn hình tồn.
- Chuông ở thanh trên của trang quản trị liệt kê các thông báo nhận được từ lúc mở trang.
- Thu ngân chỉ nhận thông báo đơn của chi nhánh mình.

## Câu hỏi thường gặp

| Câu hỏi | Trả lời |
| --- | --- |
| Vì sao không sửa được tồn bằng tay? | Mọi thay đổi tồn phải có chứng từ: phiếu nhập, đơn, phiếu chuyển, phiên kiểm kê. Muốn chỉnh tồn thì kiểm kê |
| Vì sao không hủy được đơn đã duyệt? | Đơn đã duyệt là hàng đã xuất kho. Hệ thống chưa có quy trình trả hàng ([ADR-0005](../decisions/0005-order-lifecycle-and-transactions.md)) |
| Vì sao vừa tạo sản phẩm mà tạo đơn báo "chưa được đồng bộ tới kho"? | Dữ liệu sản phẩm tới kho qua hàng đợi, trễ vài giây. Hệ thống tự thử lại; nếu vẫn báo thì chờ chút rồi thử lại |
| Nhân viên không thấy giá vốn, lợi nhuận? | Đúng thiết kế: chỉ chủ cửa hàng xem được |
| Giá vốn của đơn cũ có đổi khi nhập lô giá khác? | Không. Giá vốn chốt vào đơn lúc duyệt và giữ nguyên |
