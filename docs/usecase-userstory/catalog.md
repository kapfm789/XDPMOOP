# Use case: sản phẩm

Bốn use case của service `catalog`. Thiết kế: [data model](../design/data-model/catalog.md), [API](../design/api/catalog.md).

## UC-PROD-01 Quản lý danh mục và thương hiệu

- Actor: Owner, Staff
- Yêu cầu: FR-PROD-01
- API: `/api/catalog/categories`, `/api/catalog/brands`

Là nhân viên, tôi muốn sắp sản phẩm vào danh mục nhiều cấp và gắn thương hiệu, để tìm và báo cáo theo nhóm hàng.

Tiêu chí chấp nhận:

1. Cho một danh mục, khi tạo danh mục con của nó, thì cây danh mục có thêm một cấp; không giới hạn số cấp.
2. Cho một danh mục, khi đặt cha của nó là chính nó hoặc là con cháu của nó, thì trả 400.
3. Cho danh mục đang có sản phẩm hoặc có danh mục con, khi xóa, thì trả 409.
4. Cho tên thương hiệu đã có trong tenant, khi tạo trùng, thì trả 409.

## UC-PROD-02 Quản lý sản phẩm và SKU

- Actor: Owner, Staff
- Yêu cầu: FR-PROD-02
- API: `/api/catalog/products`, `/api/catalog/products/{id}/skus`
- Event: `SkuUpserted`

Là nhân viên, tôi muốn khai báo sản phẩm đơn và sản phẩm có biến thể, để mỗi đơn vị hàng quản lý tồn có một mã SKU riêng.

Tiêu chí chấp nhận:

1. Cho sản phẩm đơn, khi tạo, thì có đúng một SKU.
2. Cho sản phẩm có biến thể, khi tạo, thì mỗi tổ hợp thuộc tính (ví dụ đen, M) là một SKU có mã riêng.
3. Cho mã SKU đã có trong tenant, khi tạo trùng, thì trả 409; cùng mã ở tenant khác vẫn tạo được.
4. Cho bất kỳ lần tạo hoặc sửa SKU nào, khi lưu xong, thì `SkuUpserted` được phát với `version` lớn hơn lần trước.
5. Cho SKU bị ngừng bán, khi tìm ở POS, thì không hiện; tồn và lịch sử của SKU giữ nguyên.

## UC-PROD-03 Quản lý mã vạch

- Actor: Owner, Staff
- Yêu cầu: FR-PROD-03
- API: `POST /api/catalog/skus/{id}/barcodes`

Là nhân viên, tôi muốn mỗi SKU có mã vạch, để quét ở POS và khi kiểm kê.

Tiêu chí chấp nhận:

1. Cho yêu cầu tự sinh, khi tạo mã vạch EAN-13, thì mã có 13 chữ số, bắt đầu bằng tiền tố lưu hành nội bộ 200 và có số kiểm tra đúng.
2. Cho mã EAN-13 nhập tay có số kiểm tra sai, khi lưu, thì trả 400.
3. Cho mã Code128 nhập tay, khi lưu, thì chấp nhận chuỗi chữ và số tới 48 ký tự.
4. Cho mã vạch đã có trong tenant, khi gán cho SKU khác, thì trả 409.
5. Cho mã vạch đã gán, khi quét ở POS, thì ra đúng một SKU.

## UC-PROD-04 Đặt giá lẻ, giá sỉ

- Actor: Owner
- Yêu cầu: FR-PROD-04
- API: `PUT /api/catalog/skus/{id}/prices`

Là Owner, tôi muốn đặt giá bán lẻ và giá bán sỉ cho từng SKU, để giá niêm yết tách khỏi giá vốn.

Tiêu chí chấp nhận:

1. Cho giá lẻ và giá sỉ không âm, khi lưu, thì giá mới có hiệu lực cho đơn tạo sau đó.
2. Cho giá âm, khi lưu, thì trả 400.
3. Cho Staff, khi đặt giá, thì trả 403.
4. Cho đơn đã tạo trước khi đổi giá, khi xem lại, thì đơn giá trên dòng đơn không đổi.
5. Cho giá vừa đổi, khi `core` nhận `SkuUpserted`, thì POS thấy giá mới; giá vốn không bị ảnh hưởng.
