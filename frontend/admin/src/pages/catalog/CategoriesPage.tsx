import { Typography } from 'antd';
import { CategoriesPanel } from '../../features/catalog/CategoriesPanel';

export function CategoriesPage() {
  return (
    <>
      <Typography.Title level={3}>Danh mục</Typography.Title>
      <CategoriesPanel />
    </>
  );
}
