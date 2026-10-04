import { Typography } from 'antd';
import { ProductsPanel } from '../../features/catalog/ProductsPanel';

export function ProductsPage() {
  return (
    <>
      <Typography.Title level={3}>Sản phẩm</Typography.Title>
      <ProductsPanel />
    </>
  );
}
