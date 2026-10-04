import {
  confirmPurchaseReceipt,
  createPurchaseReceipt,
  createSupplier,
  errorMessage,
  formatDateTime,
  formatMoney,
  listPurchaseReceipts,
  listSuppliers,
  type PurchaseReceipt,
  type PurchaseReceiptItem,
  type PurchaseReceiptStatus,
  updatePurchaseReceipt,
} from '@oism/shared';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Flex, Form, Input, InputNumber, message, Popconfirm, Select, Space, Table, Tag } from 'antd';
import { useState } from 'react';
import { FormModal } from '../common/FormModal';
import { SkuSelect, type SkuOption } from './SkuSelect';
import { useBranchOptions } from './useBranchOptions';

type Filter = { branchId?: string; status?: PurchaseReceiptStatus; page: number };

type LineValues = { sku: SkuOption; quantity: number; unitCost: number };

type Values = { branchId: string; supplierId: string; note?: string; items: LineValues[] };

type SupplierValues = { name: string; phone?: string };

const PAGE_SIZE = 20;

const STATUS_OPTIONS: { value: PurchaseReceiptStatus; label: string }[] = [
  { value: 'Draft', label: 'Nháp' },
  { value: 'Confirmed', label: 'Đã nhập kho' },
];

// UC-INV-02: phiếu nhập nháp rồi xác nhận. Tồn và giá vốn chỉ đổi khi xác nhận, và do backend tính.
export function ReceiptsPanel() {
  const queryClient = useQueryClient();
  const [filter, setFilter] = useState<Filter>({ page: 1 });
  const [editing, setEditing] = useState<PurchaseReceipt | 'new' | null>(null);
  const [addingSupplier, setAddingSupplier] = useState(false);
  const branches = useBranchOptions();
  const suppliers = useQuery({ queryKey: ['suppliers'], queryFn: listSuppliers });
  const receipts = useQuery({
    queryKey: ['purchase-receipts', filter],
    queryFn: () => listPurchaseReceipts({ ...filter, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });
  const error = receipts.error ?? suppliers.error ?? branches.error;
  const supplierNames = new Map((suppliers.data ?? []).map((supplier) => [supplier.id, supplier.name]));
  const narrow = (change: Partial<Filter>) => setFilter((current) => ({ ...current, ...change, page: 1 }));
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['purchase-receipts'] });

  const confirm = useMutation({
    mutationFn: (receipt: PurchaseReceipt) => confirmPurchaseReceipt(receipt.id),
    onSuccess: async (receipt) => {
      void message.success(`Đã nhập kho phiếu ${receipt.receiptNumber}`);
      // Xác nhận làm đổi tồn, giá vốn và thêm dòng sổ.
      await Promise.all(['purchase-receipts', 'stock', 'ledger'].map((key) => queryClient.invalidateQueries({ queryKey: [key] })));
    },
    onError: (failure) => {
      void message.error(errorMessage(failure));
      // Trạng thái có thể đã đổi ở nơi khác: tải lại danh sách.
      void refresh();
    },
  });

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
            onChange={(status?: PurchaseReceiptStatus) => narrow({ status })}
            style={{ width: 180 }}
          />
        </Flex>
        <Space>
          <Button onClick={() => setAddingSupplier(true)}>Thêm nhà cung cấp</Button>
          <Button type="primary" onClick={() => setEditing('new')}>
            Tạo phiếu nhập
          </Button>
        </Space>
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<PurchaseReceipt>
        scroll={{ x: 'max-content' }}
        rowKey="id"
        loading={receipts.isPending}
        dataSource={receipts.data?.items}
        pagination={{
          current: filter.page,
          pageSize: PAGE_SIZE,
          total: receipts.data?.total,
          showSizeChanger: false,
          onChange: (page) => setFilter((current) => ({ ...current, page })),
        }}
        expandable={{
          expandedRowRender: (receipt) => (
            <Table<PurchaseReceiptItem>
              rowKey="id"
              size="small"
              pagination={false}
              dataSource={receipt.items}
              columns={[
                { title: 'Mã SKU', dataIndex: 'skuCode' },
                { title: 'Tên', dataIndex: 'skuName' },
                { title: 'Số lượng', dataIndex: 'quantity', align: 'right' },
                { title: 'Đơn giá nhập', dataIndex: 'unitCost', align: 'right', render: (unitCost: number) => formatMoney(unitCost) },
              ]}
            />
          ),
        }}
        columns={[
          { title: 'Mã phiếu', dataIndex: 'receiptNumber' },
          { title: 'Chi nhánh', dataIndex: 'branchId', render: (id: string) => branches.name(id) },
          { title: 'Nhà cung cấp', dataIndex: 'supplierId', render: (id: string) => supplierNames.get(id) ?? '' },
          { title: 'Số dòng', align: 'right', render: (_, receipt) => receipt.items.length },
          { title: 'Ngày tạo', dataIndex: 'createdAt', render: (createdAt: string) => formatDateTime(createdAt) },
          {
            title: 'Trạng thái',
            dataIndex: 'status',
            render: (status: PurchaseReceiptStatus) =>
              status === 'Confirmed' ? <Tag color="green">Đã nhập kho</Tag> : <Tag>Nháp</Tag>,
          },
          { title: 'Ghi chú', dataIndex: 'note' },
          {
            render: (_, receipt) =>
              receipt.status === 'Draft' && (
                <Space>
                  <Button type="link" onClick={() => setEditing(receipt)}>
                    Sửa
                  </Button>
                  <Popconfirm
                    title="Xác nhận nhập kho?"
                    description="Tồn và giá vốn sẽ được cập nhật. Phiếu đã xác nhận không sửa được."
                    okText="Xác nhận"
                    cancelText="Hủy"
                    onConfirm={() => confirm.mutate(receipt)}
                  >
                    <Button type="link" loading={confirm.isPending && confirm.variables.id === receipt.id}>
                      Xác nhận nhập kho
                    </Button>
                  </Popconfirm>
                </Space>
              ),
          },
        ]}
      />
      {editing && (
        <FormModal<Values>
          title={editing === 'new' ? 'Tạo phiếu nhập' : `Sửa phiếu nhập ${editing.receiptNumber}`}
          width={760}
          initialValues={
            editing === 'new'
              ? undefined
              : {
                  branchId: editing.branchId,
                  supplierId: editing.supplierId,
                  note: editing.note ?? undefined,
                  items: editing.items.map((item) => ({
                    sku: { value: item.skuId, label: `${item.skuCode} · ${item.skuName}` },
                    quantity: item.quantity,
                    unitCost: item.unitCost,
                  })),
                }
          }
          submit={async ({ branchId, items, ...rest }) => {
            const request = { ...rest, items: items.map(({ sku, ...line }) => ({ skuId: sku.value, ...line })) };
            await (editing === 'new' ? createPurchaseReceipt({ branchId, ...request }) : updatePurchaseReceipt(editing.id, request));
            await refresh();
          }}
          onClose={() => {
            setEditing(null);
            // Phiếu có thể đã được xác nhận ở nơi khác trong lúc đang sửa.
            void refresh();
          }}
        >
          <Form.Item name="branchId" label="Chi nhánh nhận hàng" rules={[{ required: true, message: 'Chọn chi nhánh' }]}>
            <Select options={editing === 'new' ? branches.activeOptions : branches.options} disabled={editing !== 'new'} />
          </Form.Item>
          <Form.Item label="Nhà cung cấp" required>
            <Flex gap={8}>
              <Form.Item name="supplierId" noStyle rules={[{ required: true, message: 'Chọn nhà cung cấp' }]}>
                <Select
                  showSearch={{ optionFilterProp: 'label' }}
                  options={(suppliers.data ?? []).map((supplier) => ({ value: supplier.id, label: supplier.name }))}
                />
              </Form.Item>
              <Button onClick={() => setAddingSupplier(true)}>Thêm mới</Button>
            </Flex>
          </Form.Item>
          <Form.Item name="note" label="Ghi chú">
            <Input maxLength={500} />
          </Form.Item>
          {/* Phiếu mới mở sẵn một dòng trống. */}
          <Form.List name="items" initialValue={editing === 'new' ? [{ quantity: 1 }] : undefined}>
            {(fields, { add, remove }) => (
              <Flex vertical gap={8}>
                {fields.map((field) => (
                  <Flex key={field.key} gap={8} align="start">
                    <Form.Item name={[field.name, 'sku']} rules={[{ required: true, message: 'Chọn SKU' }]} style={{ flex: 1, marginBottom: 0 }}>
                      <SkuSelect />
                    </Form.Item>
                    <Form.Item name={[field.name, 'quantity']} rules={[{ required: true, message: 'Nhập số lượng' }]} style={{ marginBottom: 0 }}>
                      <InputNumber min={1} precision={0} placeholder="Số lượng" aria-label="Số lượng" style={{ width: 110 }} />
                    </Form.Item>
                    <Form.Item name={[field.name, 'unitCost']} rules={[{ required: true, message: 'Nhập đơn giá' }]} style={{ marginBottom: 0 }}>
                      <InputNumber min={0} placeholder="Đơn giá nhập" aria-label="Đơn giá nhập" suffix="₫" style={{ width: 170 }} />
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
      {addingSupplier && (
        <FormModal<SupplierValues>
          title="Thêm nhà cung cấp"
          submit={async (values) => {
            await createSupplier(values);
            await queryClient.invalidateQueries({ queryKey: ['suppliers'] });
          }}
          onClose={() => setAddingSupplier(false)}
        >
          <Form.Item name="name" label="Tên" rules={[{ required: true, whitespace: true, message: 'Nhập tên nhà cung cấp' }]}>
            <Input autoFocus maxLength={200} />
          </Form.Item>
          <Form.Item name="phone" label="Số điện thoại">
            <Input maxLength={20} />
          </Form.Item>
        </FormModal>
      )}
    </>
  );
}
