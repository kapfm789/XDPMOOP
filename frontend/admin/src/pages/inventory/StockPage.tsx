import { Typography } from 'antd';
import { StockPanel } from '../../features/inventory/StockPanel';

export function StockPage() {
  return (
    <>
      <Typography.Title level={3}>Tồn kho</Typography.Title>
      <StockPanel />
    </>
  );
}
