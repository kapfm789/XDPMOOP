// Kiểu dữ liệu của API catalog: docs/design/api/catalog.md.

export type Category = {
  id: string;
  name: string;
  parentId: string | null;
  sortOrder: number;
};

// Một nút của cây danh mục trả về từ GET /categories.
export type CategoryNode = Category & { children: CategoryNode[] };

export type CategoryRequest = {
  name: string;
  parentId?: string | null;
};

export type Brand = {
  id: string;
  name: string;
};

export type BarcodeSymbology = 'EAN13' | 'Code128';

export type Barcode = {
  id: string;
  code: string;
  symbology: BarcodeSymbology;
};

// `name` là tên hiển thị: tên sản phẩm kèm các giá trị thuộc tính.
export type Sku = {
  id: string;
  productId: string;
  skuCode: string;
  name: string;
  attributes: Record<string, string>;
  retailPrice: number;
  wholesalePrice: number;
  isActive: boolean;
  barcodes: Barcode[];
};

// Sản phẩm kèm mọi SKU, mã vạch và giá: GET /products/{id}.
export type Product = {
  id: string;
  name: string;
  categoryId: string;
  brandId?: string | null;
  description?: string | null;
  hasVariants: boolean;
  isActive: boolean;
  skus: Sku[];
};

// Một dòng của GET /products.
export type ProductListItem = Omit<Product, 'description' | 'skus'> & { skuCount: number };

export type ProductFilter = {
  query?: string;
  categoryId?: string;
  brandId?: string;
  page: number;
  pageSize: number;
};

// Một SKU gửi lên khi tạo sản phẩm hoặc thêm SKU. Giá do Staff gửi bị backend bỏ qua.
export type SkuRequest = {
  skuCode: string;
  attributes?: Record<string, string>;
  retailPrice?: number;
  wholesalePrice?: number;
};

export type ProductRequest = {
  name: string;
  categoryId: string;
  brandId?: string | null;
  description?: string | null;
};

export type CreateProductRequest = ProductRequest & { hasVariants: boolean; skus: SkuRequest[] };

export type UpdateProductRequest = ProductRequest & { isActive: boolean };

export type UpdateSkuRequest = {
  attributes: Record<string, string>;
  isActive: boolean;
};

export type AddBarcodeRequest = {
  symbology: BarcodeSymbology;
  // Bỏ trống với EAN13 thì server tự sinh.
  code?: string;
};

export type SetPricesRequest = {
  retailPrice: number;
  wholesalePrice: number;
};
