# API: catalog

Tiền tố `/api/catalog`. Use case ở [catalog.md](../../usecase-userstory/catalog.md); bảng ở [data-model/catalog.md](../data-model/catalog.md). Quy ước chung ở [README.md](README.md).

## Danh mục và thương hiệu

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/catalog/categories` | Owner, Staff | | Cây danh mục | |
| POST | `/api/catalog/categories` | Owner, Staff | `name`, `parentId?` | 201: danh mục | 409 `duplicate` |
| PUT | `/api/catalog/categories/{id}` | Owner, Staff | `name`, `parentId?` | 200: danh mục | 400 khi tạo vòng lặp cha con |
| DELETE | `/api/catalog/categories/{id}` | Owner, Staff | | 204 | 409 `category_in_use` khi còn sản phẩm hoặc danh mục con |
| GET | `/api/catalog/brands` | Owner, Staff | | Danh sách thương hiệu | |
| POST | `/api/catalog/brands` | Owner, Staff | `name` | 201: thương hiệu | 409 `duplicate` |
| PUT | `/api/catalog/brands/{id}` | Owner, Staff | `name` | 200: thương hiệu | 409 `duplicate` |

- Danh mục trả về gồm `id`, `name`, `parentId`, `sortOrder`. Cây danh mục là mảng các danh mục gốc, mỗi nút có thêm `children`; các nút cùng cha xếp theo `sortOrder` rồi `name`.
- Thương hiệu trả về gồm `id`, `name`; danh sách xếp theo `name`.
- `name` bị cắt khoảng trắng hai đầu, không được rỗng và dài tối đa 200 ký tự; sai thì 400 `validation_failed`.
- Tạo hoặc sửa danh mục trùng tên với một danh mục cùng cha (kể cả cùng là danh mục gốc) trả 409 `duplicate`. `parentId` không có trong tenant trả 404.

## Sản phẩm và SKU

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/catalog/products` | Owner, Staff | `query?`, `categoryId?`, `brandId?`, `page`, `pageSize` | Danh sách sản phẩm kèm số SKU | |
| GET | `/api/catalog/products/{id}` | Owner, Staff | | Sản phẩm kèm mọi SKU, mã vạch, giá | |
| POST | `/api/catalog/products` | Owner, Staff | `name`, `categoryId`, `brandId?`, `description?`, `hasVariants`, `skus[]` | 201: sản phẩm | 409 `duplicate` khi trùng mã SKU |
| PUT | `/api/catalog/products/{id}` | Owner, Staff | `name`, `categoryId`, `brandId?`, `description?`, `isActive` | 200: sản phẩm | |
| POST | `/api/catalog/products/{id}/skus` | Owner, Staff | `skuCode`, `attributes`, `retailPrice?`, `wholesalePrice?` | 201: SKU | 409 `duplicate` |
| PUT | `/api/catalog/skus/{id}` | Owner, Staff | `attributes`, `isActive` | 200: SKU | |

Mỗi phần tử của `skus[]` khi tạo sản phẩm gồm `skuCode`, `attributes`, `retailPrice?`, `wholesalePrice?`. Sản phẩm có `hasVariants = false` nhận đúng một phần tử. Staff gửi giá khi tạo thì giá bị bỏ qua và đặt bằng 0; chỉ Owner đặt được giá.

## Mã vạch và giá

| Phương thức | Đường dẫn | Vai trò | Vào | Ra | Lỗi riêng |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/catalog/skus/{id}/barcodes` | Owner, Staff | `symbology`, `code?` | 201: mã vạch | 400 khi EAN-13 sai số kiểm tra; 409 `duplicate` |
| DELETE | `/api/catalog/skus/{id}/barcodes/{barcodeId}` | Owner, Staff | | 204 | |
| PUT | `/api/catalog/skus/{id}/prices` | Owner | `retailPrice`, `wholesalePrice` | 200: SKU | 400 khi giá âm |

Bỏ trống `code` với `symbology = EAN13` thì server tự sinh. Mọi thay đổi SKU, mã vạch hoặc giá phát `SkuUpserted` với trạng thái đầy đủ của SKU ([events.md](../events.md)).
