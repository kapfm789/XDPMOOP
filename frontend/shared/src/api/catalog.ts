import type { Brand, Category, CategoryNode, CategoryRequest } from '../types/catalog';
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
