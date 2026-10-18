import { errorMessage, formatNumber, type LowStockAlert } from '@oism/shared';
import { Alert, Card, Table, Typography } from 'antd';
import { Link } from 'react-router-dom';
import { StockLevelTag } from '../inventory/StockLevelTag';
import { useBranchOptions } from '../inventory/useBranchOptions';
import { useLowStockAlerts } from './useDashboardData';

// UC-REP-03: SKU có tồn khả dụng bằng hoặc dưới ngưỡng; tự tải lại khi có LowStock và mỗi phút.
export function LowStockAlertsCard() {
  const branches = useBranchOptions();
  const alerts = useLowStockAlerts();
  const error = alerts.error ?? branches.error;

  return (
    <Card title="Cảnh báo tồn thấp" extra={<Link to="/inventory/stock">Xem tồn kho</Link>} style={{ height: '100%' }}>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<LowStockAlert>
        size="small"
        scroll={{ x: 'max-content' }}
        rowKey={(row) => `${row.branchId}-${row.skuId}`}
        loading={alerts.isPending}
        dataSource={alerts.data}
        pagination={{ pageSize: 8, showSizeChanger: false, hideOnSinglePage: true }}
        locale={{ emptyText: 'Không có SKU nào dưới ngưỡng' }}
        columns={[
          {
            title: 'SKU',
            render: (_, row) =>
              row.skuCode ? (
                <>
                  <Typography.Text strong>{row.skuCode}</Typography.Text>
                  <br />
                  <Typography.Text type="secondary">{row.name}</Typography.Text>
                </>
              ) : (
                row.skuId
              ),
          },
          { title: 'Chi nhánh', dataIndex: 'branchId', render: (id: string) => branches.name(id) },
          { title: 'Khả dụng', dataIndex: 'available', align: 'right', render: (available: number) => formatNumber(available) },
          { title: 'Ngưỡng', dataIndex: 'threshold', align: 'right' },
          { title: 'Mức tồn', render: (_, row) => <StockLevelTag available={row.available} threshold={row.threshold} /> },
        ]}
      />
    </Card>
  );
}
