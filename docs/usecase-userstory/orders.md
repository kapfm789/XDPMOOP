# Use case: đơn hàng

Sáu use case của module Orders trong `core`. Thiết kế: [máy trạng thái](../design/state-machines.md), [data model](../design/data-model/core.md), [API](../design/api/core.md).

## UC-ORD-01 Tiếp nhận đơn online và giữ hàng

- Actor: Simulator, đóng vai sàn
- Yêu cầu: FR-ORD-01, FR-RSE-02, NFR-PERF-02
- Vào qua: command `SubmitOrder` từ `channel`
- Thiết kế: [nhận webhook](../design/flows/webhook-ingestion.md), [giữ hàng](../design/flows/reserve-stock.md)

Là chủ cửa hàng bán trên sàn, tôi muốn đơn từ mọi kênh vào chung một nơi và được giữ hàng ngay, để không bán một món cho hai người.

Tiêu chí chấp nhận:

1. Cho đơn hợp lệ và đủ hàng, khi tiếp nhận, thì đơn ở Reserved, `reserved` tăng đúng số lượng từng SKU, mỗi dòng có một phần giữ hàng Active hết hạn sau 30 phút, và `OrderReserved` được phát.
2. Cho bước giữ hàng thành công, thì `on_hand` không đổi và không có dòng sổ nào được ghi.
3. Cho đơn nhiều SKU mà một SKU thiếu hàng, khi tiếp nhận, thì cả đơn bị từ chối, không SKU nào bị giữ, và `OrderRejected` được phát kèm SKU thiếu (T04).
4. Cho cùng mã đơn của sàn tới lần hai, khi tiếp nhận, thì không có đơn mới và không giữ thêm hàng (T03).
5. Cho 50 đơn đồng thời cùng mua 1 đơn vị còn lại, khi tiếp nhận, thì đúng 1 đơn ở Reserved, 49 đơn bị từ chối và tồn khả dụng không âm (T01).
6. Cho SKU chưa có ở `core` hoặc đã ngừng bán, khi tiếp nhận, thì đơn bị từ chối với lý do nêu rõ SKU.
7. Cho đơn từ Shopee, TikTok hay Lazada, khi vào `core`, thì đều có cùng một cấu trúc Canonical Order.

## UC-ORD-02 Tạo đơn thủ công

- Actor: Owner, Staff
- Yêu cầu: FR-ORD-01, FR-RSE-02
- API: `POST /api/core/orders`
- Thiết kế: [giữ hàng](../design/flows/reserve-stock.md)

Là nhân viên, tôi muốn tạo đơn cho khách đặt qua điện thoại hoặc tin nhắn, để đơn đó cũng được giữ hàng như đơn từ sàn.

Tiêu chí chấp nhận:

1. Cho đơn hợp lệ và đủ hàng, khi tạo, thì đơn mang kênh Admin, ở Reserved, và hàng được giữ như UC-ORD-01 AC-1.
2. Cho một SKU thiếu hàng, khi tạo, thì trả 409 kèm SKU và tồn khả dụng hiện tại; không đơn nào được lưu.
3. Cho dòng đơn không nhập đơn giá, khi tạo, thì đơn giá là giá lẻ hiện tại của SKU; nhân viên nhập được đơn giá khác.
4. Cho chi nhánh đã tắt, khi tạo đơn, thì trả 409.

## UC-ORD-03 Duyệt đơn

- Actor: Owner, Staff
- Yêu cầu: FR-ORD-03, FR-RSE-03, FR-COST-02, NFR-SEC-02
- API: `POST /api/core/orders/{id}/confirm`
- Thiết kế: [xác nhận đơn](../design/flows/confirm-order.md)

Là nhân viên xử lý đơn, tôi muốn duyệt một đơn đang giữ hàng, để hàng được xuất kho và giá vốn được chốt.

Tiêu chí chấp nhận:

1. Cho đơn ở Reserved, khi duyệt, thì đơn sang Confirmed, `on_hand` và `reserved` cùng giảm đúng số lượng, tồn khả dụng không đổi.
2. Cho bước duyệt thành công, thì mỗi dòng đơn có `CostPrice` bằng giá vốn bình quân lúc duyệt và sổ có một dòng OUT lý do Sale cho mỗi dòng đơn.
3. Cho đơn không ở Reserved, khi duyệt, thì trả 409 và không gì thay đổi.
4. Cho lỗi khi ghi sổ, khi duyệt, thì đơn, tồn, giá vốn và sổ đều giữ nguyên như trước (T05).
5. Cho thao tác duyệt và job hết hạn chạy cùng lúc trên một đơn, thì chỉ một bên thành công; không có chuyện vừa xuất hàng vừa giải phóng phần giữ (T07).
6. Cho đơn đã duyệt, khi sau đó nhập lô hàng mới với giá khác, thì `CostPrice` của đơn không đổi (T10).

## UC-ORD-04 Hủy đơn

- Actor: Owner, Staff
- Yêu cầu: FR-ORD-03, FR-RSE-03
- API: `POST /api/core/orders/{id}/cancel`
- Thiết kế: [hủy và hết hạn](../design/flows/cancel-expire.md)

Là nhân viên xử lý đơn, tôi muốn hủy đơn chưa xuất kho, để hàng đã giữ quay lại bán được.

Tiêu chí chấp nhận:

1. Cho đơn ở Reserved, khi hủy, thì đơn sang Cancelled, `reserved` giảm đúng bằng các phần giữ còn Active, các phần giữ sang Released, và `OrderCancelled` được phát.
2. Cho đơn đã Cancelled, khi hủy lại, thì không có tác động lần hai (T06).
3. Cho đơn ở Confirmed hoặc Completed, khi hủy, thì trả 409 và `reserved` không đổi (T08).
4. Hủy đơn không làm đổi `on_hand` và không ghi sổ.

## UC-ORD-05 Tự hủy đơn giữ hàng hết hạn

- Actor: Hệ thống
- Yêu cầu: FR-SIM-03, FR-RSE-03
- Chạy bằng: job Hangfire mỗi phút
- Thiết kế: [hủy và hết hạn](../design/flows/cancel-expire.md)

Là chủ cửa hàng, tôi muốn đơn giữ hàng quá lâu mà không ai duyệt được tự hủy, để hàng không bị treo.

Tiêu chí chấp nhận:

1. Cho đơn ở Reserved có phần giữ đã quá hạn, khi job chạy, thì đơn sang Cancelled với lý do Expired và hàng được giải phóng như UC-ORD-04 AC-1.
2. Cho job chạy lại trên cùng đơn, thì không có tác động lần hai (T06).
3. Cho đơn đã được duyệt trước khi job tới, khi job chạy, thì job bỏ qua đơn đó.
4. Cho một đơn xử lý lỗi, thì các đơn hết hạn khác vẫn được xử lý; mỗi đơn một transaction.
5. Job xử lý mỗi đơn trong tenant context của chính đơn đó.

## UC-ORD-06 Hoàn tất đơn và in phiếu giao

- Actor: Owner, Staff
- Yêu cầu: FR-ORD-02, FR-ORD-03
- API: `POST /api/core/orders/{id}/complete`, `GET /api/core/orders/{id}`

Là nhân viên xử lý đơn, tôi muốn in phiếu giao và đánh dấu đơn đã giao xong, để theo dõi đơn tới cuối vòng đời.

Tiêu chí chấp nhận:

1. Cho đơn ở Confirmed, khi hoàn tất, thì đơn sang Completed; tồn và sổ không đổi.
2. Cho đơn không ở Confirmed, khi hoàn tất, thì trả 409.
3. Cho đơn ở Confirmed hoặc Completed, khi bấm in phiếu giao, thì hộp thoại in của trình duyệt mở với mã đơn, kênh, danh sách hàng và số lượng.
4. Cho danh sách đơn, khi lọc theo trạng thái, kênh, chi nhánh và khoảng thời gian, thì chỉ hiện đơn khớp bộ lọc.
