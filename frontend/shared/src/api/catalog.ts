import type {
  AddBarcodeRequest,
  Barcode,
  Brand,
  Category,
  CategoryNode,
  CategoryRequest,
  CreateProductRequest,
  Product,
  ProductFilter,
  ProductListItem,
  SetPricesRequest,
  Sku,
  SkuRequest,
  UpdateProductRequest,
  UpdateSkuRequest,
} from '../types/catalog';
import type { Paged } from '../types/common';
import { api } from './client';

export const getCategoryTree = () => api<CategoryNode[]>('/api/catalog/categories');

export const createCategory = (request: CategoryRequest) =>
  api<Category>('/api/catalog/categories', { method: 'POST', body: request });

export const updateCategory = (id: string, request: CategoryRequest) =>
  api<Category>(`/api/catalog/categories/${id}`, { method: 'PUT', body: request });

export const deleteCategory = (id: string) => api<void>(`/api/catalog/categories/${id}`, { method: 'DELETE' });

export const listBrands = () => api<Brand[]>('/api/catalog/brands');

export const createBrand = (name: string) => api<Brand>('/api/catalog/brands', { method: 'POST', body: { name } });

export const updateBrand = (id: string, name: string) =>
  api<Brand>(`/api/catalog/brands/${id}`, { method: 'PUT', body: { name } });

export function listProducts({ query, categoryId, brandId, page, pageSize }: ProductFilter) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (query?.trim()) params.set('query', query.trim());
  if (categoryId) params.set('categoryId', categoryId);
  if (brandId) params.set('brandId', brandId);
  return api<Paged<ProductListItem>>(`/api/catalog/products?${params}`);
}

export const getProduct = (id: string) => api<Product>(`/api/catalog/products/${id}`);

export const createProduct = (request: CreateProductRequest) =>
  api<Product>('/api/catalog/products', { method: 'POST', body: request });

export const updateProduct = (id: string, request: UpdateProductRequest) =>
  api<Product>(`/api/catalog/products/${id}`, { method: 'PUT', body: request });

export const addSku = (productId: string, request: SkuRequest) =>
  api<Sku>(`/api/catalog/products/${productId}/skus`, { method: 'POST', body: request });

export const updateSku = (id: string, request: UpdateSkuRequest) =>
  api<Sku>(`/api/catalog/skus/${id}`, { method: 'PUT', body: request });

export const addBarcode = (skuId: string, request: AddBarcodeRequest) =>
  api<Barcode>(`/api/catalog/skus/${skuId}/barcodes`, { method: 'POST', body: request });

export const removeBarcode = (skuId: string, barcodeId: string) =>
  api<void>(`/api/catalog/skus/${skuId}/barcodes/${barcodeId}`, { method: 'DELETE' });

// Chỉ Owner gọi được: docs/design/api/catalog.md mục "Mã vạch và giá".
export const setPrices = (skuId: string, request: SetPricesRequest) =>
  api<Sku>(`/api/catalog/skus/${skuId}/prices`, { method: 'PUT', body: request });
