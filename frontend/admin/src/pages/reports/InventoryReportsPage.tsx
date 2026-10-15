import { Typography } from 'antd';
import { InventoryReportsPanel } from '../../features/reports/InventoryReportsPanel';

export function InventoryReportsPage() {
  return (
    <>
      <Typography.Title level={3}>Báo cáo tồn kho</Typography.Title>
      <InventoryReportsPanel />
    </>
  );
}
