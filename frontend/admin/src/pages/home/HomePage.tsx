import { useAuth } from '@oism/shared';
import { Col, Flex, Row, Typography } from 'antd';
import { PageHeader } from '../../app/PageHeader';
import { DashboardStats } from '../../features/dashboard/DashboardStats';
import { LowStockAlertsCard } from '../../features/dashboard/LowStockAlertsCard';
import { PendingOrdersCard } from '../../features/dashboard/PendingOrdersCard';
import { RevenueChart } from '../../features/dashboard/RevenueChart';

// Tổng quan: thẻ số liệu, biểu đồ doanh thu 7 ngày (chỉ Owner), cảnh báo tồn và đơn chờ duyệt (docs/design/ui/admin.md).
// Owner: biểu đồ bên trái cao bằng cột phải; cột phải là đơn chờ duyệt rồi cảnh báo tồn thấp. Staff không có biểu đồ nên
// hai thẻ nằm cạnh nhau.
export function HomePage() {
  const { user } = useAuth();

  return (
    <>
      <PageHeader tags={<Typography.Text type="secondary">Xin chào, {user?.fullName}</Typography.Text>} />
      <DashboardStats />
      {user?.role === 'Owner' ? (
        <Row gutter={[16, 16]} align="stretch">
          <Col xs={24} xl={13}>
            <RevenueChart fill />
          </Col>
          <Col xs={24} xl={11}>
            <Flex vertical gap={16}>
              <PendingOrdersCard />
              <LowStockAlertsCard />
            </Flex>
          </Col>
        </Row>
      ) : (
        <Row gutter={[16, 16]}>
          <Col xs={24} xl={12}>
            <PendingOrdersCard />
          </Col>
          <Col xs={24} xl={12}>
            <LowStockAlertsCard />
          </Col>
        </Row>
      )}
    </>
  );
}
