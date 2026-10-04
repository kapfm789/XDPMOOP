import { errorMessage, formatMoney, listStock, setStockThreshold, type StockRow, useAuth } from '@oism/shared';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Flex, Form, Input, InputNumber, Select, Space, Table, type TableColumnsType } from 'antd';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { FormModal } from '../common/FormModal';
import { useBranchOptions } from './useBranchOptions';

type Filter = { branchId?: string; query?: string; page: number };

type ThresholdValues = { threshold?: number | null };

const PAGE_SIZE = 20;

// UC-INV-01 AC-1: tồn thực tế, đang giữ, khả dụng và giá vốn bình quân theo chi nhánh và SKU.
// Giá vốn chỉ có trong phản hồi cho Owner; giao diện không tự tính ra nó.
// UC-INV-05: đặt ngưỡng tồn tối thiểu; để trống là bỏ ngưỡng.
export function StockPanel() {
  const queryClient = useQueryClient();
  const isOwner = useAuth().user?.role === 'Owner';
  const [filter, setFilter] = useState<Filter>({ page: 1 });
  const [editing, setEditing] = useState<StockRow | null>(null);
  const branches = useBranchOptions();
  const stock = useQuery({
    queryKey: ['stock', filter],
    queryFn: () => listStock({ ...filter, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });
  const error = stock.error ?? branches.error;
  // Đổi bộ lọc thì quay về trang đầu.
  const narrow = (change: Partial<Filter>) => setFilter((current) => ({ ...current, ...change, page: 1 }));

  const columns: TableColumnsType<StockRow> = [
    { title: 'Mã SKU', dataIndex: 'skuCode' },
    { title: 'Tên', dataIndex: 'name' },
    { title: 'Chi nhánh', dataIndex: 'branchId', render: (id: string) => branches.name(id) },
    { title: 'Tồn', dataIndex: 'onHand', align: 'right' },
    { title: 'Đang giữ', dataIndex: 'reserved', align: 'right' },
    { title: 'Khả dụng', dataIndex: 'available', align: 'right' },
    ...(isOwner
      ? ([
          {
            title: 'Giá vốn bình quân',
            dataIndex: 'avgCost',
            align: 'right',
            render: (avgCost?: number) => (avgCost === undefined ? '' : formatMoney(avgCost)),
          },
        ] satisfies TableColumnsType<StockRow>)
      : []),
    { title: 'Ngưỡng tối thiểu', dataIndex: 'threshold', align: 'right' },
    {
      render: (_, row) => (
        <Space>
          <Button type="link" onClick={() => setEditing(row)}>
            Đặt ngưỡng
          </Button>
          <Link to={`/inventory/ledger?branchId=${row.branchId}&skuId=${row.skuId}`}>Xem sổ</Link>
        </Space>
      ),
    },
  ];

  return (
    <>
      <Flex gap={8} wrap style={{ marginBottom: 16 }}>
        <Select
          allowClear
          placeholder="Mọi chi nhánh"
          aria-label="Lọc theo chi nhánh"
          options={branches.options}
          value={filter.branchId}
          onChange={(branchId?: string) => narrow({ branchId })}
          style={{ width: 240 }}
        />
        <Input.Search
          allowClear
          placeholder="Tìm theo mã hoặc tên SKU"
          aria-label="Tìm SKU"
          onSearch={(query) => narrow({ query })}
          style={{ width: 280 }}
        />
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<StockRow>
        scroll={{ x: 'max-content' }}
        rowKey={(row) => `${row.branchId}:${row.skuId}`}
        loading={stock.isPending}
        dataSource={stock.data?.items}
        columns={columns}
        pagination={{
          current: filter.page,
          pageSize: PAGE_SIZE,
          total: stock.data?.total,
          showSizeChanger: false,
          onChange: (page) => setFilter((current) => ({ ...current, page })),
        }}
      />
      {editing && (
        <FormModal<ThresholdValues>
          title={`Ngưỡng tồn tối thiểu của ${editing.skuCode}`}
          initialValues={{ threshold: editing.threshold }}
          submit={async ({ threshold }) => {
            await setStockThreshold({ branchId: editing.branchId, skuId: editing.skuId, threshold: threshold ?? null });
            await queryClient.invalidateQueries({ queryKey: ['stock'] });
          }}
          onClose={() => setEditing(null)}
        >
          <Form.Item name="threshold" label="Ngưỡng" extra="Để trống thì SKU này không sinh cảnh báo tồn thấp.">
            <InputNumber autoFocus min={0} precision={0} style={{ width: '100%' }} />
          </Form.Item>
        </FormModal>
      )}
    </>
  );
}
