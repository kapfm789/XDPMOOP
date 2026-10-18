import { formatMoney, formatNumber, getInventoryValue, useAuth } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';
import { Col, Row } from 'antd';
import { StatCard } from '../common/StatCard';
import { useLowStockAlerts } from '../dashboard/useDashboardData';

type Props = { branchId?: string; trackedRows?: number; loading: boolean };

// Dải tổng hợp trên bảng tồn: tổng giá trị tồn (chỉ Owner), số dòng tồn đang theo dõi, số SKU tồn thấp
// (docs/design/ui/admin.md). Giá trị tồn là `totalValue` của insights; giao diện không tự nhân tồn với giá vốn.
export function StockSummary({ branchId, trackedRows, loading }: Props) {
  const isOwner = useAuth().user?.role === 'Owner';
  const value = useQuery({
    queryKey: ['inventory-value', 'total', branchId ?? 'all'],
    queryFn: () => getInventoryValue({ branchId, page: 1, pageSize: 1 }),
    enabled: isOwner,
  });
  const alerts = useLowStockAlerts(branchId);
  const lowSkus = new Set(alerts.data?.map((alert) => alert.skuId)).size;
  const span = isOwner ? 8 : 12;

  return (
    <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
      {isOwner && (
        <Col xs={24} md={span}>
          <StatCard
            label="Tổng giá trị tồn"
            icon="report"
            tone="info"
            loading={value.isPending}
            value={value.data?.totalValue == null ? '—' : formatMoney(value.data.totalValue)}
            hint="Theo giá vốn bình quân"
          />
        </Col>
      )}
      <Col xs={24} md={span}>
        <StatCard
          label="Dòng tồn đang theo dõi"
          icon="warehouse"
          tone="success"
          loading={loading}
          value={trackedRows === undefined ? '—' : formatNumber(trackedRows)}
          hint="Cặp chi nhánh và SKU khớp bộ lọc"
        />
      </Col>
      <Col xs={24} md={span}>
        <StatCard
          label="SKU tồn thấp"
          icon="bell"
          tone={lowSkus > 0 ? 'error' : 'success'}
          loading={alerts.isPending}
          value={alerts.data ? formatNumber(lowSkus) : '—'}
          hint="Tồn khả dụng bằng hoặc dưới ngưỡng"
        />
      </Col>
    </Row>
  );
}
