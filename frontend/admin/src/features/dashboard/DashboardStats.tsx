import { formatMoney, formatNumber, getGrossProfit, useAuth, vietnamDate } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';
import { Col, Row } from 'antd';
import { StatCard } from '../common/StatCard';
import { useLowStockAlerts, usePendingOrders } from './useDashboardData';

// Thẻ số liệu của trang tổng quan: doanh thu hôm nay (chỉ Owner), số đơn chờ duyệt, số SKU tồn thấp
// (docs/design/ui/admin.md). Mỗi số là kết quả của một API đã có; giao diện không tự tính doanh thu.
export function DashboardStats() {
  const isOwner = useAuth().user?.role === 'Owner';
  const today = vietnamDate(new Date());
  const revenue = useQuery({
    queryKey: ['gross-profit', 'today', today],
    queryFn: () => getGrossProfit({ from: today, to: today }),
    enabled: isOwner,
  });
  const pending = usePendingOrders();
  const alerts = useLowStockAlerts();
  const lowSkus = new Set(alerts.data?.map((alert) => alert.skuId)).size;
  const span = isOwner ? 8 : 12;

  return (
    <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
      {isOwner && (
        <Col xs={24} md={span}>
          <StatCard
            label="Doanh thu hôm nay"
            icon="report"
            tone="info"
            loading={revenue.isPending}
            value={revenue.data ? formatMoney(revenue.data.totals.revenue) : '—'}
            hint={revenue.data && `Lợi nhuận gộp ${formatMoney(revenue.data.totals.grossProfit)}`}
          />
        </Col>
      )}
      <Col xs={24} md={span}>
        <StatCard
          label="Đơn chờ duyệt"
          icon="order"
          tone="pending"
          loading={pending.isPending}
          value={pending.data ? formatNumber(pending.data.total) : '—'}
          hint="Đơn đang giữ hàng, chưa duyệt"
        />
      </Col>
      <Col xs={24} md={span}>
        <StatCard
          label="SKU tồn thấp"
          icon="warehouse"
          tone={lowSkus > 0 ? 'error' : 'success'}
          loading={alerts.isPending}
          value={alerts.data ? formatNumber(lowSkus) : '—'}
          hint="Tồn khả dụng bằng hoặc dưới ngưỡng"
        />
      </Col>
    </Row>
  );
}
