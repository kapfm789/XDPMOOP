import { Typography } from 'antd';
import { TransfersPanel } from '../../features/inventory/TransfersPanel';

export function TransfersPage() {
  return (
    <>
      <Typography.Title level={3}>Chuyển kho</Typography.Title>
      <TransfersPanel />
    </>
  );
}
