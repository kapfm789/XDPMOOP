// Danh sách có phân trang: docs/design/api/README.md mục "Dữ liệu".
export type Paged<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
};
