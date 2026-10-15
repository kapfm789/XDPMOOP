import { errorMessage, formatNumber, listSlowMovers, listTopSellers, type SlowMoverRow, type TopSellerRow } from '@oism/shared';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Alert, Table } from 'antd';
import { skuLabel } from './skuLabel';

type Props = { kind: 'top' | 'slow'; branchId?: string; days: number };

// Bán chạy xếp giảm dần theo số bán; bán chậm chỉ gồm SKU còn tồn, SKU không bán được gì đứng đầu (ADR-0008).
export function SellersTable({ kind, branchId, days }: Props) {
  const filter = { branchId, days, limit: 20 };
  const rows = useQuery({
    queryKey: [kind === 'top' ? 'top-sellers' : 'slow-movers', filter],
    queryFn: (): Promise<(TopSellerRow | SlowMoverRow)[]> => (kind === 'top' ? listTopSellers(filter) : listSlowMovers(filter)),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      {rows.error && <Alert type="error" showIcon title={errorMessage(rows.error)} style={{ marginBottom: 16 }} />}
      <Table<TopSellerRow | SlowMoverRow>
        scroll={{ x: 'max-content' }}
        rowKey="skuId"
        loading={rows.isFetching}
        dataSource={rows.data}
        pagination={false}
        locale={{ emptyText: kind === 'top' ? 'Chưa bán được SKU nào trong khoảng này' : 'Không có SKU nào còn tồn' }}
        columns={[
          { title: '#', render: (_, __, index) => index + 1, width: 48 },
          { title: 'SKU', render: (_, row) => skuLabel(row) },
          { title: 'Số bán', dataIndex: 'quantity', align: 'right', render: (quantity: number) => formatNumber(quantity) },
          ...(kind === 'slow'
            ? [{ title: 'Tồn hiện tại', align: 'right' as const, render: (_: unknown, row: TopSellerRow | SlowMoverRow) => ('onHand' in row ? formatNumber(row.onHand) : '') }]
            : []),
        ]}
      />
    </>
  );
}
