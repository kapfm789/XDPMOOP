import { Typography } from 'antd';
import { ReceiptsPanel } from '../../features/inventory/ReceiptsPanel';

export function ReceiptsPage() {
  return (
    <>
      <Typography.Title level={3}>Phiếu nhập</Typography.Title>
      <ReceiptsPanel />
    </>
  );
}
