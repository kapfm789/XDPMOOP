import { useAuth } from '@oism/shared';
import { Col, Row, Typography } from 'antd';
import { PageHeader } from '../../app/PageHeader';
import { DashboardStats } from '../../features/dashboard/DashboardStats';
import { LowStockAlertsCard } from '../../features/dashboard/LowStockAlertsCard';
import { PendingOrdersCard } from '../../features/dashboard/PendingOrdersCard';
import { RevenueChart } from '../../features/dashboard/RevenueChart';

// Tổng quan: thẻ số liệu, biểu đồ doanh thu 7 ngày (chỉ Owner), cảnh báo tồn và đơn chờ duyệt (docs/design/ui/admin.md).
export function HomePage() {
  const { user } = useAuth();

  return (
    <>
      <PageHeader tags={<Typography.Text type="secondary">Xin chào, {user?.fullName}</Typography.Text>} />
      <DashboardStats />
      {user?.role === 'Owner' && (
        <div style={{ marginBottom: 16 }}>
          <RevenueChart />
        </div>
      )}
      <Row gutter={[16, 16]}>
        <Col xs={24} xl={12}>
          <LowStockAlertsCard />
        </Col>
        <Col xs={24} xl={12}>
          <PendingOrdersCard />
        </Col>
      </Row>
    </>
  );
}
