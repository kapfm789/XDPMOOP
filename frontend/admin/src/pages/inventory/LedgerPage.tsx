import { Typography } from 'antd';
import { LedgerPanel } from '../../features/inventory/LedgerPanel';

export function LedgerPage() {
  return (
    <>
      <Typography.Title level={3}>Sổ giao dịch</Typography.Title>
      <LedgerPanel />
    </>
  );
}
