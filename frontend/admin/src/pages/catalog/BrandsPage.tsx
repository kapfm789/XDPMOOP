import { Typography } from 'antd';
import { BrandsPanel } from '../../features/catalog/BrandsPanel';

export function BrandsPage() {
  return (
    <>
      <Typography.Title level={3}>Thương hiệu</Typography.Title>
      <BrandsPanel />
    </>
  );
}
