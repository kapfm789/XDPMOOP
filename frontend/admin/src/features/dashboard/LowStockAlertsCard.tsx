import { errorMessage, formatNumber, type LowStockAlert, listLowStockAlerts } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Table } from 'antd';
import { Link } from 'react-router-dom';
import { useBranchOptions } from '../inventory/useBranchOptions';

// UC-REP-03: SKU có tồn khả dụng bằng hoặc dưới ngưỡng. Thông báo LowStock làm danh sách tải lại ngay; SKU lên lại trên
// ngưỡng thì không có thông báo nào, nên danh sách cũng tự tải lại mỗi phút.
export function LowStockAlertsCard() {
  const branches = useBranchOptions();
  const alerts = useQuery({ queryKey: ['low-stock'], queryFn: () => listLowStockAlerts(), refetchInterval: 60_000 });
  const error = alerts.error ?? branches.error;

  return (
    <Card title="Cảnh báo tồn thấp" extra={<Link to="/inventory/stock">Xem tồn kho</Link>}>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<LowStockAlert>
        size="small"
        scroll={{ x: 'max-content' }}
        rowKey={(row) => `${row.branchId}-${row.skuId}`}
        loading={alerts.isPending}
        dataSource={alerts.data}
        pagination={{ pageSize: 10, showSizeChanger: false, hideOnSinglePage: true }}
        locale={{ emptyText: 'Không có SKU nào dưới ngưỡng' }}
        columns={[
          { title: 'SKU', render: (_, row) => (row.skuCode ? `${row.skuCode} · ${row.name}` : row.skuId) },
          { title: 'Chi nhánh', dataIndex: 'branchId', render: (id: string) => branches.name(id) },
          { title: 'Khả dụng', dataIndex: 'available', align: 'right', render: (available: number) => formatNumber(available) },
          { title: 'Ngưỡng', dataIndex: 'threshold', align: 'right' },
        ]}
      />
    </Card>
  );
}
