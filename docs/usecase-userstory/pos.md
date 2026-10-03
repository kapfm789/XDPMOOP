# Use case: bán tại quầy

Ba use case của ứng dụng `pos` và phần POS trong `core`. Thiết kế: [luồng checkout](../design/flows/pos-checkout.md), [màn hình POS](../design/ui/pos.md), [API](../design/api/core.md).

## UC-POS-01 Tìm hoặc quét sản phẩm

- Actor: Cashier, Owner
- Yêu cầu: FR-POS-01, FR-POS-02, NFR-PERF-01
- API: `GET /api/core/pos/skus`

Là thu ngân, tôi muốn tìm sản phẩm bằng tên, mã SKU hoặc quét mã vạch, để thêm hàng vào giỏ thật nhanh.

Tiêu chí chấp nhận:

1. Cho từ khóa là một phần tên, mã SKU hoặc mã vạch, khi tìm, thì kết quả kèm giá lẻ và tồn khả dụng tại chi nhánh của thu ngân.
2. Cho mã vạch khớp đúng một SKU, khi quét, thì SKU được thêm thẳng vào giỏ với số lượng 1.
3. Cho SKU có tồn khả dụng bằng 0, khi tìm, thì hiện là hết hàng và không thêm được vào giỏ.
4. Cho số lượng trong giỏ đã bằng tồn khả dụng, khi tăng thêm, thì giao diện chặn và báo số còn lại.
5. Cho SKU đã ngừng bán, khi tìm, thì không hiện.
6. Cho tải đã thống nhất, khi đo, thì p95 của lời gọi tìm dưới 200 ms (T17).
7. Ô tìm kiếm luôn giữ focus để quét liên tiếp mà không cần chạm màn hình.

## UC-POS-02 Thanh toán tại quầy

- Actor: Cashier, Owner
- Yêu cầu: FR-POS-03, FR-POS-04, FR-COST-02, NFR-SEC-02, NFR-USA-02
- API: `POST /api/core/pos/checkout` với header `Idempotency-Key`
- Thiết kế: [luồng checkout](../design/flows/pos-checkout.md)

Là thu ngân, tôi muốn bấm một lần để hoàn tất đơn, để khách không phải chờ và tồn kho đúng ngay.

Tiêu chí chấp nhận:

1. Cho giỏ hợp lệ và thu ngân đã xác nhận nhận tiền mặt hoặc chuyển khoản QR, khi thanh toán, thì trong một transaction: đơn được tạo ở Completed, `on_hand` giảm, giá vốn được chốt trên từng dòng, sổ có dòng OUT lý do Sale, và thanh toán được ghi.
2. Cho một SKU hết tồn khả dụng vào lúc thanh toán, dù lúc thêm vào giỏ còn hàng, thì trả 409 kèm SKU và tồn khả dụng hiện tại; không gì được lưu.
3. Cho cùng `Idempotency-Key` gửi hai lần, thì chỉ có một đơn và lần sau nhận lại đúng đơn đó (T03).
4. Cho POS và một đơn online cùng mua đơn vị cuối, thì tổng số được chấp nhận không vượt tồn khả dụng (T02).
5. Cho hàng đang được giữ cho đơn online, khi thanh toán ở POS, thì phần hàng đó không bán được.
6. Cho lỗi ở bất kỳ bước ghi nào, thì toàn bộ rollback (T05).
7. Cho Cashier, khi thanh toán cho chi nhánh khác chi nhánh trong token, thì trả 403.
8. Cho đơn một sản phẩm, tiền mặt, không giảm giá, thì hoàn tất bằng quét mã rồi Enter, hoặc dưới 3 lần nhấp.

## UC-POS-03 In hóa đơn

- Actor: Cashier, Owner
- Yêu cầu: FR-POS-03

Là thu ngân, tôi muốn hóa đơn tự mở hộp thoại in sau khi thanh toán, để đưa cho khách ngay.

Tiêu chí chấp nhận:

1. Cho backend đã trả đơn lưu thành công, thì hộp thoại in của trình duyệt mở với mã đơn, thời gian, danh sách hàng, đơn giá, tổng tiền và phương thức thanh toán.
2. Cho in lỗi hoặc thu ngân đóng hộp thoại, thì không có đơn mới nào được tạo.
3. Cho đơn vừa bán, khi bấm in lại, thì in từ đơn đã có mà không gọi thanh toán lần nữa.
