# Use case: kho

Năm use case của module Inventory trong `core`. Thiết kế: [data model](../design/data-model/core.md), [API](../design/api/core.md), [bất biến và khóa](../architecture/transactions-and-concurrency.md).

## UC-INV-01 Xem tồn và sổ giao dịch

- Actor: Owner, Staff
- Yêu cầu: FR-INV-01, FR-RSE-01, NFR-SEC-03
- API: `GET /api/core/stock`, `GET /api/core/ledger`

Là nhân viên kho, tôi muốn xem tồn hiện tại và lịch sử mọi biến động, để biết còn bao nhiêu bán được và vì sao con số thay đổi.

Tiêu chí chấp nhận:

1. Cho một SKU tại một chi nhánh, khi xem tồn, thì thấy `on_hand`, `reserved`, `available = on_hand - reserved` và giá vốn bình quân.
2. Cho bộ lọc chi nhánh, SKU, khoảng thời gian, khi xem sổ, thì các dòng hiện theo thứ tự ghi, mỗi dòng có loại IN hoặc OUT, lý do, số lượng, số dư sau giao dịch, chứng từ và người tạo.
3. Cho bất kỳ SKU nào, khi cộng IN trừ OUT của sổ, thì bằng `on_hand`.
4. Không có API hay màn hình nào sửa hoặc xóa dòng sổ; lệnh `UPDATE` hoặc `DELETE` trực tiếp bị database từ chối (T16).

## UC-INV-02 Nhập hàng từ nhà cung cấp

- Actor: Owner, Staff
- Yêu cầu: FR-INV-02, FR-COST-01
- API: `POST /api/core/purchase-receipts`, `POST /api/core/purchase-receipts/{id}/confirm`
- Thiết kế: [luồng nhập hàng](../design/flows/purchase-receipt.md)

Là nhân viên kho, tôi muốn ghi nhận hàng nhập từ nhà cung cấp, để tồn tăng và giá vốn được cập nhật theo giá nhập.

Tiêu chí chấp nhận:

1. Cho phiếu ở Draft, khi sửa hoặc xóa dòng, thì tồn chưa bị tác động.
2. Cho phiếu Draft hợp lệ, khi xác nhận, thì `on_hand` tăng đúng số lượng và mỗi dòng phiếu sinh một dòng sổ IN với lý do Purchase.
3. Cho tồn 10 với giá vốn 100.000, khi xác nhận nhập 5 với đơn giá 130.000, thì giá vốn mới là 110.000 và tồn là 15 (T09).
4. Cho SKU chưa từng có ở chi nhánh, khi xác nhận nhập, thì giá vốn bằng đúng đơn giá nhập.
5. Cho phiếu đã Confirmed, khi xác nhận lại, thì tồn và giá vốn không đổi lần hai (T11).
6. Cho số lượng không dương hoặc đơn giá âm, khi lưu, thì trả 400.
7. Cho lỗi ở bất kỳ dòng nào khi xác nhận, thì không dòng nào được ghi.

## UC-INV-03 Chuyển kho giữa hai chi nhánh

- Actor: Owner, Staff
- Yêu cầu: FR-INV-03
- API: `POST /api/core/transfers`, `/transfers/{id}/ship`, `/transfers/{id}/receive`, `DELETE /api/core/transfers/{id}`
- Thiết kế: [luồng chuyển kho](../design/flows/stock-transfer.md)

Là nhân viên kho, tôi muốn chuyển hàng từ chi nhánh này sang chi nhánh khác qua hai bước, để hàng đang đi đường không bị bán ở nơi nào.

Tiêu chí chấp nhận:

1. Cho nơi gửi đủ tồn khả dụng, khi xuất, thì `on_hand` nơi gửi giảm, sổ có dòng OUT lý do TransferOut và phiếu sang InTransit.
2. Cho nơi gửi không đủ tồn khả dụng, khi xuất, thì trả 409 và không gì thay đổi.
3. Cho phiếu đang InTransit, khi xem tồn nơi nhận, thì tồn khả dụng nơi nhận chưa tăng (T12).
4. Cho phiếu đang InTransit, khi nơi nhận xác nhận, thì `on_hand` nơi nhận tăng, sổ có dòng IN lý do TransferIn và giá vốn nơi nhận được tính lại bằng đơn giá mang theo.
5. Cho mọi thời điểm, thì tồn nơi gửi cộng hàng đang vận chuyển cộng tồn nơi nhận không đổi.
6. Cho phiếu đã Received, khi nhận lại, thì không có tác động lần hai.
7. Cho nơi gửi trùng nơi nhận, khi tạo phiếu, thì trả 400.
8. Cho phiếu còn Draft, khi xóa, thì phiếu biến mất và tồn không bị tác động; cho phiếu đã InTransit hoặc Received, khi xóa, thì trả 409.

## UC-INV-04 Kiểm kê

- Actor: Owner, Staff
- Yêu cầu: FR-INV-04
- API: `POST /api/core/stocktakes`, `PUT /stocktakes/{id}/counts`, `POST /stocktakes/{id}/post`
- Thiết kế: [luồng kiểm kê](../design/flows/stocktake.md)

Là nhân viên kho, tôi muốn nhập số đếm thực tế và để hệ thống tự điều chỉnh, để tồn trên sổ khớp với hàng trong kho.

Tiêu chí chấp nhận:

1. Cho phiên đang Open, khi nhập số đếm, thì tồn chưa bị tác động.
2. Cho số đếm khác số trên sổ, khi chốt, thì sổ có dòng IN hoặc OUT lý do StocktakeAdjust bằng đúng chênh lệch và `on_hand` bằng số đếm.
3. Cho số đếm bằng số trên sổ, khi chốt, thì không có dòng sổ nào được ghi cho SKU đó.
4. Cho số đếm nhỏ hơn `reserved` của một SKU, khi chốt, thì trả 409 kèm danh sách đơn đang giữ SKU đó và không SKU nào bị điều chỉnh (T13).
5. Cho phiên đã Posted, khi chốt lại, thì không có tác động lần hai.
6. Kiểm kê không làm đổi giá vốn bình quân.

## UC-INV-05 Đặt ngưỡng tồn tối thiểu

- Actor: Owner, Staff
- Yêu cầu: FR-REP-03
- API: `PUT /api/core/stock/threshold`

Là nhân viên kho, tôi muốn đặt ngưỡng tồn tối thiểu cho SKU tại chi nhánh, để được cảnh báo khi cần nhập thêm.

Tiêu chí chấp nhận:

1. Cho ngưỡng không âm, khi lưu, thì `StockChanged` được phát mang ngưỡng mới.
2. Cho ngưỡng để trống, khi lưu, thì SKU đó không còn sinh cảnh báo.
3. Cho ngưỡng âm, khi lưu, thì trả 400.
