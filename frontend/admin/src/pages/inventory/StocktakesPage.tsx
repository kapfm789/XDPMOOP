import { Typography } from 'antd';
import { StocktakesPanel } from '../../features/inventory/StocktakesPanel';

export function StocktakesPage() {
  return (
    <>
      <Typography.Title level={3}>Kiểm kê</Typography.Title>
      <StocktakesPanel />
    </>
  );
}
