import {
  ApiError,
  errorMessage,
  formatDateTime,
  listStock,
  listStocktakes,
  openStocktake,
  postStocktake,
  setStocktakeCounts,
  type Stocktake,
  type StocktakeItem,
  type StocktakeShortfall,
  type StocktakeStatus,
} from '@oism/shared';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Flex, Form, InputNumber, message, Popconfirm, Select, Space, Table, Tag, Typography } from 'antd';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { FormModal } from '../common/FormModal';
import { SkuSelect, type SkuOption } from './SkuSelect';
import { useBranchOptions } from './useBranchOptions';

type Filter = { branchId?: string; status?: StocktakeStatus; page: number };

type CountLine = { sku: SkuOption; countedQty: number };

type CountValues = { items: CountLine[] };

const PAGE_SIZE = 20;

const STATUS_OPTIONS: { value: StocktakeStatus; label: string }[] = [
  { value: 'Open', label: 'Đang đếm' },
  { value: 'Posted', label: 'Đã chốt' },
];

// UC-INV-04: mở phiên, nhập số đếm, chốt. Tồn chỉ đổi khi chốt, và chênh lệch do backend tính dưới khóa.
export function StocktakesPanel() {
  const queryClient = useQueryClient();
  const [filter, setFilter] = useState<Filter>({ page: 1 });
  const [opening, setOpening] = useState(false);
  const [counting, setCounting] = useState<Stocktake | null>(null);
  const branches = useBranchOptions();
  const stocktakes = useQuery({
    queryKey: ['stocktakes', filter],
    queryFn: () => listStocktakes({ ...filter, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });
  const narrow = (change: Partial<Filter>) => setFilter((current) => ({ ...current, ...change, page: 1 }));
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['stocktakes'] });

  const post = useMutation({
    mutationFn: (stocktake: Stocktake) => postStocktake(stocktake.id),
    onSuccess: async (stocktake) => {
      const adjusted = stocktake.items.filter((item) => item.difference).length;
      void message.success(`Đã chốt kiểm kê ${branches.name(stocktake.branchId)}: điều chỉnh ${adjusted} SKU`);
      // Chốt làm đổi tồn và thêm dòng sổ.
      await Promise.all(['stocktakes', 'stock', 'ledger'].map((key) => queryClient.invalidateQueries({ queryKey: [key] })));
    },
    // Phiên có thể đã được chốt ở nơi khác: tải lại danh sách.
    onError: () => void refresh(),
  });
  const error = stocktakes.error ?? branches.error;

  return (
    <>
      <Flex justify="space-between" gap={16} wrap style={{ marginBottom: 16 }}>
        <Flex gap={8} wrap>
          <Select
            allowClear
            placeholder="Mọi chi nhánh"
            aria-label="Lọc theo chi nhánh"
            options={branches.options}
            value={filter.branchId}
            onChange={(branchId?: string) => narrow({ branchId })}
            style={{ width: 240 }}
          />
          <Select
            allowClear
            placeholder="Mọi trạng thái"
            aria-label="Lọc theo trạng thái"
            options={STATUS_OPTIONS}
            value={filter.status}
            onChange={(status?: StocktakeStatus) => narrow({ status })}
            style={{ width: 180 }}
          />
        </Flex>
        <Button type="primary" onClick={() => setOpening(true)}>
          Mở phiên kiểm kê
        </Button>
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      {post.error && (
        <Alert
          type="error"
          showIcon
          closable
          onClose={() => post.reset()}
          title={errorMessage(post.error)}
          description={belowReservedLines(post.error, post.variables)}
          style={{ marginBottom: 16 }}
        />
      )}
      <Table<Stocktake>
        scroll={{ x: 'max-content' }}
        rowKey="id"
        loading={stocktakes.isPending}
        dataSource={stocktakes.data?.items}
        pagination={{
          current: filter.page,
          pageSize: PAGE_SIZE,
          total: stocktakes.data?.total,
          showSizeChanger: false,
          onChange: (page) => setFilter((current) => ({ ...current, page })),
        }}
        expandable={{
          expandedRowRender: (stocktake) => (
            <Table<StocktakeItem>
              rowKey="id"
              size="small"
              pagination={false}
              dataSource={stocktake.items}
              locale={{ emptyText: 'Chưa nhập số đếm nào' }}
              columns={[
                { title: 'Mã SKU', dataIndex: 'skuCode' },
                { title: 'Tên', dataIndex: 'skuName' },
                { title: 'Số đếm', dataIndex: 'countedQty', align: 'right' },
                { title: 'Trên sổ lúc chốt', dataIndex: 'systemQty', align: 'right' },
                {
                  title: 'Chênh lệch',
                  dataIndex: 'difference',
                  align: 'right',
                  render: (difference?: number | null) =>
                    difference === null || difference === undefined ? (
                      ''
                    ) : (
                      <Typography.Text type={difference < 0 ? 'danger' : difference > 0 ? 'success' : undefined}>
                        {difference > 0 ? `+${difference}` : difference}
                      </Typography.Text>
                    ),
                },
              ]}
            />
          ),
        }}
        columns={[
          { title: 'Chi nhánh', dataIndex: 'branchId', render: (id: string) => branches.name(id) },
          { title: 'Số SKU đã đếm', align: 'right', render: (_, stocktake) => stocktake.items.length },
          {
            title: 'Trạng thái',
            dataIndex: 'status',
            render: (status: StocktakeStatus) => (status === 'Posted' ? <Tag color="green">Đã chốt</Tag> : <Tag color="blue">Đang đếm</Tag>),
          },
          { title: 'Chốt lúc', dataIndex: 'postedAt', render: (at?: string | null) => (at ? formatDateTime(at) : '') },
          {
            render: (_, stocktake) =>
              stocktake.status === 'Open' && (
                <Space>
                  <Button type="link" onClick={() => setCounting(stocktake)}>
                    Nhập số đếm
                  </Button>
                  <Popconfirm
                    title="Chốt phiên kiểm kê?"
                    description="Tồn của các SKU đã đếm được đặt bằng số đếm và sổ ghi dòng điều chỉnh. Phiên đã chốt không sửa được."
                    okText="Chốt"
                    cancelText="Hủy"
                    onConfirm={() => post.mutate(stocktake)}
                  >
                    <Button type="link" loading={post.isPending && post.variables.id === stocktake.id}>
                      Chốt
                    </Button>
                  </Popconfirm>
                </Space>
              ),
          },
        ]}
      />
      {opening && (
        <FormModal<{ branchId: string }>
          title="Mở phiên kiểm kê"
          submit={async ({ branchId }) => {
            setCounting(await openStocktake(branchId));
            await refresh();
          }}
          onClose={() => setOpening(false)}
        >
          <Form.Item name="branchId" label="Chi nhánh" rules={[{ required: true, message: 'Chọn chi nhánh' }]}>
            <Select options={branches.activeOptions} />
          </Form.Item>
        </FormModal>
      )}
      {counting && (
        <FormModal<CountValues>
          title={`Nhập số đếm · ${branches.name(counting.branchId)}`}
          width={720}
          initialValues={{
            items: counting.items.map((item) => ({
              sku: { value: item.skuId, label: `${item.skuCode} · ${item.skuName}` },
              countedQty: item.countedQty,
            })),
          }}
          submit={async ({ items = [] }) => {
            await setStocktakeCounts(counting.id, items.map(({ sku, countedQty }) => ({ skuId: sku.value, countedQty })));
            await refresh();
          }}
          onClose={() => {
            setCounting(null);
            // Phiên có thể đã được chốt ở nơi khác trong lúc đang đếm.
            void refresh();
          }}
        >
          <Typography.Paragraph type="secondary">
            Lưu thay cả bộ số đếm của phiên. SKU không có trong danh sách không bị điều chỉnh khi chốt.
          </Typography.Paragraph>
          <Form.List name="items">
            {(fields, { add, remove }) => (
              <Flex vertical gap={8}>
                {fields.map((field) => (
                  <Flex key={field.key} gap={8} align="start">
                    <Form.Item name={[field.name, 'sku']} rules={[{ required: true, message: 'Chọn SKU' }]} style={{ flex: 1, marginBottom: 0 }}>
                      <SkuSelect />
                    </Form.Item>
                    <Form.Item name={[field.name, 'countedQty']} rules={[{ required: true, message: 'Nhập số đếm' }]} style={{ marginBottom: 0 }}>
                      <InputNumber min={0} precision={0} placeholder="Số đếm" aria-label="Số đếm" style={{ width: 120 }} />
                    </Form.Item>
                    <Button type="link" danger onClick={() => remove(field.name)}>
                      Xóa
                    </Button>
                  </Flex>
                ))}
                <Flex gap={8}>
                  <Button type="dashed" onClick={() => add()} style={{ flex: 1 }}>
                    Thêm dòng
                  </Button>
                  <AddBranchSkus branchId={counting.branchId} add={add} />
                </Flex>
              </Flex>
            )}
          </Form.List>
        </FormModal>
      )}
    </>
  );
}

type AddBranchSkusProps = { branchId: string; add: (line: Partial<CountLine>) => void };

// Thêm một dòng trống cho mỗi SKU đang có số dư ở chi nhánh mà danh sách chưa có, để đếm hết kho không phải chọn từng SKU.
function AddBranchSkus({ branchId, add }: AddBranchSkusProps) {
  const form = Form.useFormInstance<CountValues>();
  const load = useMutation({
    mutationFn: async () => {
      const lines: (CountLine | undefined)[] = form.getFieldValue('items') ?? [];
      const present = new Set(lines.flatMap((line) => (line?.sku ? [line.sku.value] : [])));
      let added = 0;
      for (let page = 1; ; page++) {
        const stock = await listStock({ branchId, page, pageSize: 100 });
        for (const row of stock.items.filter((row) => !present.has(row.skuId))) {
          add({ sku: { value: row.skuId, label: `${row.skuCode} · ${row.name}` } });
          added++;
        }
        if (page * stock.pageSize >= stock.total) return added;
      }
    },
    onSuccess: (added) => void message.info(added ? `Đã thêm ${added} SKU` : 'Mọi SKU của chi nhánh đã có trong danh sách'),
    onError: (failure) => void message.error(errorMessage(failure)),
  });

  return (
    <Button loading={load.isPending} onClick={() => load.mutate()}>
      Thêm mọi SKU có ở chi nhánh
    </Button>
  );
}

// 409 `stocktake_below_reserved`: SKU đếm thấp hơn lượng đang giữ và các đơn đang giữ nó, để nhân viên xử lý đơn rồi chốt lại.
function belowReservedLines(error: unknown, stocktake?: Stocktake) {
  if (!(error instanceof ApiError) || error.code !== 'stocktake_below_reserved' || !Array.isArray(error.details)) return undefined;

  const names = new Map((stocktake?.items ?? []).map((item) => [item.skuId, `${item.skuCode} · ${item.skuName}`]));
  return (
    <ul style={{ margin: 0, paddingLeft: 20 }}>
      {(error.details as StocktakeShortfall[]).map((shortfall) => (
        <li key={shortfall.skuId}>
          {names.get(shortfall.skuId) ?? shortfall.skuId}: đếm {shortfall.countedQty}, đang giữ {shortfall.reserved} cho đơn{' '}
          {shortfall.orders.map((order, index) => (
            <span key={order.orderId}>
              {index > 0 && ', '}
              <Link to={`/orders/${order.orderId}`}>{order.orderNumber}</Link> ({order.channel}, {order.quantity})
            </span>
          ))}
        </li>
      ))}
    </ul>
  );
}
