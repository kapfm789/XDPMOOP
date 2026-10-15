import { useAuth } from '@oism/shared';
import { Col, Row, Typography } from 'antd';
import { LowStockAlertsCard } from '../../features/dashboard/LowStockAlertsCard';
import { PendingOrdersCard } from '../../features/dashboard/PendingOrdersCard';

// Tổng quan: cảnh báo tồn và đơn chờ duyệt (docs/design/ui/admin.md).
export function HomePage() {
  const { user } = useAuth();

  return (
    <>
      <Typography.Title level={3}>Tổng quan</Typography.Title>
      <Typography.Paragraph>Xin chào, {user?.fullName}.</Typography.Paragraph>
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
