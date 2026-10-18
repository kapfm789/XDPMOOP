import {
  ApiError,
  createTransfer,
  deleteTransfer,
  errorMessage,
  formatDateTime,
  formatMoney,
  listTransfers,
  type StockShortage,
  type StockTransfer,
  type TransferItem,
  type TransferStatus,
  transitionTransfer,
  useAuth,
} from '@oism/shared';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Flex, Form, InputNumber, message, Popconfirm, Select, Space, Table, type TableColumnsType } from 'antd';
import { useState } from 'react';
import { FilterBar } from '../common/FilterBar';
import { FormModal } from '../common/FormModal';
import { DocumentCode, StatusTag, type Tone } from '../common/StatusTag';
import { SkuSelect, type SkuOption } from './SkuSelect';
import { useBranchOptions } from './useBranchOptions';

type Filter = { status?: TransferStatus; page: number };

type LineValues = { sku: SkuOption; quantity: number };

type Values = { fromBranchId: string; toBranchId: string; items: LineValues[] };

type Action = { transfer: StockTransfer; action: 'ship' | 'receive' | 'delete' };

const PAGE_SIZE = 20;

const STATUS: Record<TransferStatus, { label: string; tone: Tone }> = {
  Draft: { label: 'Nháp', tone: 'neutral' },
  InTransit: { label: 'Đang vận chuyển', tone: 'pending' },
  Received: { label: 'Đã nhận', tone: 'success' },
};

// UC-INV-03: chuyển kho hai bước. Xuất trừ tồn nơi gửi, nhận cộng tồn nơi nhận; backend quyết định đủ tồn hay không.
export function TransfersPanel() {
  const queryClient = useQueryClient();
  const isOwner = useAuth().user?.role === 'Owner';
  const [filter, setFilter] = useState<Filter>({ page: 1 });
  const [creating, setCreating] = useState(false);
  const branches = useBranchOptions();
  const transfers = useQuery({
    queryKey: ['transfers', filter],
    queryFn: () => listTransfers({ ...filter, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['transfers'] });

  const act = useMutation({
    mutationFn: async ({ transfer, action }: Action) => {
      await (action === 'delete' ? deleteTransfer(transfer.id) : transitionTransfer(transfer.id, action));
    },
    onSuccess: async (_, { transfer, action }) => {
      const done = { ship: 'Đã xuất kho', receive: 'Đã nhận hàng', delete: 'Đã xóa' }[action];
      void message.success(`${done} phiếu ${transfer.transferNumber}`);
      // Xuất và nhận làm đổi tồn và thêm dòng sổ.
      await Promise.all(['transfers', 'stock', 'ledger'].map((key) => queryClient.invalidateQueries({ queryKey: [key] })));
    },
    // Trạng thái có thể đã đổi ở nơi khác: tải lại danh sách.
    onError: () => void refresh(),
  });
  const error = transfers.error ?? branches.error;

  const itemColumns: TableColumnsType<TransferItem> = [
    { title: 'Mã SKU', dataIndex: 'skuCode' },
    { title: 'Tên', dataIndex: 'skuName' },
    { title: 'Số lượng', dataIndex: 'quantity', align: 'right' },
    // Giá vốn mang theo chỉ có sau khi xuất và chỉ có trong phản hồi cho Owner.
    ...(isOwner
      ? ([
          {
            title: 'Giá vốn mang theo',
            dataIndex: 'unitCost',
            align: 'right',
            render: (unitCost?: number) => (unitCost === undefined ? '' : formatMoney(unitCost)),
          },
        ] satisfies TableColumnsType<TransferItem>)
      : []),
  ];

  return (
    <>
      <FilterBar>
        <Select
          allowClear
          placeholder="Mọi trạng thái"
          aria-label="Lọc theo trạng thái"
          options={Object.entries(STATUS).map(([value, { label }]) => ({ value, label }))}
          value={filter.status}
          onChange={(status?: TransferStatus) => setFilter({ status, page: 1 })}
          style={{ width: 200 }}
        />
      </FilterBar>
      <Card
        title="Danh sách phiếu chuyển kho"
        extra={
          <Button type="primary" onClick={() => setCreating(true)}>
            Tạo phiếu chuyển
          </Button>
        }
      >
        {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
        {act.error && (
          <Alert
            type="error"
            showIcon
            closable
            onClose={() => act.reset()}
            title={errorMessage(act.error)}
            description={shortageLines(act.error, act.variables?.transfer)}
            style={{ marginBottom: 16 }}
          />
        )}
        <Table<StockTransfer>
          scroll={{ x: 'max-content' }}
          rowKey="id"
          loading={transfers.isPending}
          dataSource={transfers.data?.items}
          pagination={{
            current: filter.page,
            pageSize: PAGE_SIZE,
            total: transfers.data?.total,
            showSizeChanger: false,
            onChange: (page) => setFilter((current) => ({ ...current, page })),
          }}
          expandable={{
            expandedRowRender: (transfer) => (
              <Table<TransferItem> rowKey="id" size="small" pagination={false} dataSource={transfer.items} columns={itemColumns} />
            ),
          }}
          columns={[
            { title: 'Mã phiếu', dataIndex: 'transferNumber', render: (transferNumber: string) => <DocumentCode code={transferNumber} /> },
            { title: 'Nơi gửi', dataIndex: 'fromBranchId', render: (id: string) => branches.name(id) },
            { title: 'Nơi nhận', dataIndex: 'toBranchId', render: (id: string) => branches.name(id) },
            { title: 'Số dòng', align: 'right', render: (_, transfer) => transfer.items.length },
            {
              title: 'Trạng thái',
              dataIndex: 'status',
              render: (status: TransferStatus) => <StatusTag tone={STATUS[status].tone}>{STATUS[status].label}</StatusTag>,
            },
            { title: 'Xuất lúc', dataIndex: 'shippedAt', render: (at?: string | null) => (at ? formatDateTime(at) : '') },
            { title: 'Nhận lúc', dataIndex: 'receivedAt', render: (at?: string | null) => (at ? formatDateTime(at) : '') },
            {
              render: (_, transfer) => {
                const busy = (action: Action['action']) =>
                  act.isPending && act.variables.transfer.id === transfer.id && act.variables.action === action;
                if (transfer.status === 'Draft') {
                  return (
                    <Space>
                      <Popconfirm
                        title="Xuất kho phiếu này?"
                        description="Tồn nơi gửi giảm ngay; hàng nằm ở trạng thái đang vận chuyển tới khi nơi nhận xác nhận."
                        okText="Xuất kho"
                        cancelText="Hủy"
                        onConfirm={() => act.mutate({ transfer, action: 'ship' })}
                      >
                        <Button type="link" loading={busy('ship')}>
                          Xuất kho
                        </Button>
                      </Popconfirm>
                      <Popconfirm
                        title="Xóa phiếu nháp này?"
                        okText="Xóa"
                        cancelText="Hủy"
                        onConfirm={() => act.mutate({ transfer, action: 'delete' })}
                      >
                        <Button type="link" danger loading={busy('delete')}>
                          Xóa
                        </Button>
                      </Popconfirm>
                    </Space>
                  );
                }
                return (
                  transfer.status === 'InTransit' && (
                    <Popconfirm
                      title="Xác nhận đã nhận đủ hàng?"
                      description="Tồn nơi nhận tăng và giá vốn nơi nhận được tính lại."
                      okText="Đã nhận"
                      cancelText="Hủy"
                      onConfirm={() => act.mutate({ transfer, action: 'receive' })}
                    >
                      <Button type="link" loading={busy('receive')}>
                        Nhận hàng
                      </Button>
                    </Popconfirm>
                  )
                );
              },
            },
          ]}
        />
      </Card>
      {creating && (
        <FormModal<Values>
          title="Tạo phiếu chuyển kho"
          width={720}
          submit={async ({ items, ...branchPair }) => {
            await createTransfer({ ...branchPair, items: items.map(({ sku, quantity }) => ({ skuId: sku.value, quantity })) });
            await refresh();
          }}
          onClose={() => setCreating(false)}
        >
          <Flex gap={16}>
            <Form.Item name="fromBranchId" label="Nơi gửi" rules={[{ required: true, message: 'Chọn nơi gửi' }]} style={{ flex: 1 }}>
              <Select options={branches.activeOptions} />
            </Form.Item>
            <Form.Item
              name="toBranchId"
              label="Nơi nhận"
              dependencies={['fromBranchId']}
              rules={[
                { required: true, message: 'Chọn nơi nhận' },
                // Báo sớm; backend vẫn trả 400 khi hai chi nhánh trùng nhau.
                ({ getFieldValue }) => ({
                  validator: (_, value?: string) =>
                    value && value === getFieldValue('fromBranchId')
                      ? Promise.reject(new Error('Nơi nhận phải khác nơi gửi'))
                      : Promise.resolve(),
                }),
              ]}
              style={{ flex: 1 }}
            >
              <Select options={branches.activeOptions} />
            </Form.Item>
          </Flex>
          {/* Phiếu mới mở sẵn một dòng trống. */}
          <Form.List name="items" initialValue={[{ quantity: 1 }]}>
            {(fields, { add, remove }) => (
              <Flex vertical gap={8}>
                {fields.map((field) => (
                  <Flex key={field.key} gap={8} align="start">
                    <Form.Item name={[field.name, 'sku']} rules={[{ required: true, message: 'Chọn SKU' }]} style={{ flex: 1, marginBottom: 0 }}>
                      <SkuSelect />
                    </Form.Item>
                    <Form.Item name={[field.name, 'quantity']} rules={[{ required: true, message: 'Nhập số lượng' }]} style={{ marginBottom: 0 }}>
                      <InputNumber min={1} precision={0} placeholder="Số lượng" aria-label="Số lượng" style={{ width: 120 }} />
                    </Form.Item>
                    <Button type="link" danger disabled={fields.length === 1} onClick={() => remove(field.name)}>
                      Xóa
                    </Button>
                  </Flex>
                ))}
                <Button type="dashed" onClick={() => add({ quantity: 1 })}>
                  Thêm dòng
                </Button>
              </Flex>
            )}
          </Form.List>
        </FormModal>
      )}
    </>
  );
}

// 409 `insufficient_stock` khi xuất: tên SKU và số còn lại lấy từ `details` (docs/design/ui/admin.md).
function shortageLines(error: unknown, transfer?: StockTransfer) {
  if (!(error instanceof ApiError) || error.code !== 'insufficient_stock' || !Array.isArray(error.details)) return undefined;

  const names = new Map((transfer?.items ?? []).map((item) => [item.skuId, `${item.skuCode} · ${item.skuName}`]));
  return (
    <ul style={{ margin: 0, paddingLeft: 20 }}>
      {(error.details as StockShortage[]).map((shortage) => (
        <li key={shortage.skuId}>
          {names.get(shortage.skuId) ?? shortage.skuId}: cần {shortage.requested}, nơi gửi còn {shortage.available}
        </li>
      ))}
    </ul>
  );
}
