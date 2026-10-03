# backend/services/catalog — kế hoạch folder

Quản lý danh mục, thương hiệu, sản phẩm, SKU, mã vạch và giá niêm yết (FR-PROD-01..04). Owner: Dev B. Kế hoạch tổng: [docs/PLAN.md](../PLAN.md).

## Phân lớp

```text
catalog/
├─ src/
│  ├─ Oism.Catalog.Domain/           Category, Brand, Product, Sku, Barcode, quy tắc EAN-13
│  ├─ Oism.Catalog.Application/      UpsertCategory, UpsertBrand, UpsertProduct, AddSku, GenerateBarcode, SetPrices
│  ├─ Oism.Catalog.Infrastructure/   CatalogDbContext, migration, outbox
│  └─ Oism.Catalog.Api/              CategoriesController, BrandsController, ProductsController, SkusController
└─ tests/
   ├─ Oism.Catalog.UnitTests/
   └─ Oism.Catalog.IntegrationTests/
```

Phụ thuộc chỉ hướng vào trong: Api → Application → Domain; Infrastructure cài đặt interface của Application.

## Việc theo phase

| Phase | Mã | Việc | Owner | Yêu cầu | Xong khi |
| --- | --- | --- | --- | --- | --- |
| 1 | W1-06 | catalog: danh mục phân cấp, thương hiệu | B | FR-PROD-01 | CRUD và test cây danh mục |
| 2 | W1-07 | catalog: sản phẩm, biến thể/SKU, mã vạch EAN-13/Code128, giá lẻ và giá sỉ; phát `SkuUpserted` | B | FR-PROD-02..04 | SKU trùng trong tenant bị từ chối; EAN-13 đúng số kiểm tra |
| 2 | W1-12 | Rà ERD ở `docs/design/data-model/` theo migration thật; test cách ly tenant cho identity và catalog | A (ERD), B (test) | NFR-TENANT-01 | ERD khớp migration; tenant A gọi ID của tenant B nhận 404 |
| 9 | W5-02 | Bộ test cách ly tenant toàn hệ thống: API, event, SignalR, job | A | NFR-TENANT-01 | Mọi ca dùng ID tenant khác nhận 404 hoặc 403 |

## Sở hữu

- Schema `catalog` của database `oism`: Category, Brand, Product, Sku, Barcode (mục 5 của kế hoạch tổng).
- API: `/categories`, `/brands`, `/products`, `/products/{id}/skus`, `/skus/{id}/barcodes`, `/skus/{id}/prices` (mục 8).
- Event phát: `SkuUpserted` (mục 7).
- Kiểm thử: T14; điều kiện xong của W1-06 và W1-07.

## Quy tắc riêng

- Mã SKU unique trong phạm vi tenant; mã vạch unique trong phạm vi tenant.
- Giá lẻ và giá sỉ là giá niêm yết, không phải giá vốn; giá vốn thuộc `core`.
- Mỗi lần tạo hoặc sửa SKU, mã vạch, giá đều phát lại `SkuUpserted` với trạng thái đầy đủ của SKU.
- Nếu phải lui về phương án 3 service (mục 11), folder này gộp vào `core` thành module Catalog.
