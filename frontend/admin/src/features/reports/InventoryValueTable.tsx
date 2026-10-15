import { errorMessage, formatMoney, formatNumber, getInventoryValue, type InventoryValueRow, useAuth } from '@oism/shared';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Alert, Card, Statistic, Table, type TableColumnsType } from 'antd';
import { useState } from 'react';
import { skuLabel } from './skuLabel';

const PAGE_SIZE = 20;

type Props = { branchId?: string; branchName: (id: string) => string };

// UC-REP-02 AC-1: on_hand × avg_cost theo chi nhánh và SKU, kèm tổng. Staff không nhận giá vốn và giá trị từ backend.
export function InventoryValueTable({ branchId, branchName }: Props) {
  const isOwner = useAuth().user?.role === 'Owner';
  const [page, setPage] = useState(1);
  const filter = { branchId, page, pageSize: PAGE_SIZE };
  const report = useQuery({
    queryKey: ['inventory-value', filter],
    queryFn: () => getInventoryValue(filter),
    placeholderData: keepPreviousData,
  });

  const columns: TableColumnsType<InventoryValueRow> = [
    { title: 'SKU', render: (_, row) => skuLabel(row) },
    { title: 'Chi nhánh', dataIndex: 'branchId', render: (id: string) => branchName(id) },
    { title: 'Tồn', dataIndex: 'onHand', align: 'right', render: (onHand: number) => formatNumber(onHand) },
    ...(isOwner
      ? ([
          { title: 'Giá vốn bình quân', dataIndex: 'avgCost', align: 'right', render: (cost: number | null) => (cost === null ? '' : formatMoney(cost)) },
          { title: 'Giá trị tồn', dataIndex: 'value', align: 'right', render: (value: number | null) => (value === null ? '' : formatMoney(value)) },
        ] satisfies TableColumnsType<InventoryValueRow>)
      : []),
  ];

  return (
    <>
      {report.error && <Alert type="error" showIcon title={errorMessage(report.error)} style={{ marginBottom: 16 }} />}
      {isOwner && (
        <Card style={{ marginBottom: 16, maxWidth: 360 }}>
          <Statistic
            title="Tổng giá trị tồn theo giá vốn"
            value={report.data?.totalValue == null ? '–' : formatMoney(report.data.totalValue)}
            loading={report.isPending}
          />
        </Card>
      )}
      <Table<InventoryValueRow>
        scroll={{ x: 'max-content' }}
        rowKey={(row) => `${row.branchId}-${row.skuId}`}
        loading={report.isFetching}
        dataSource={report.data?.items}
        columns={columns}
        pagination={{ current: page, pageSize: PAGE_SIZE, total: report.data?.total, showSizeChanger: false, onChange: setPage }}
      />
    </>
  );
}
