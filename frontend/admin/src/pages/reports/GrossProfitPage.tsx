import { Typography } from 'antd';
import { GrossProfitPanel } from '../../features/reports/GrossProfitPanel';

export function GrossProfitPage() {
  return (
    <>
      <Typography.Title level={3}>Doanh thu và lợi nhuận gộp</Typography.Title>
      <GrossProfitPanel />
    </>
  );
}
