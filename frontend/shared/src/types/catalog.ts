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
