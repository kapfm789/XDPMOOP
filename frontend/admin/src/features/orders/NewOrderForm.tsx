import { ApiError, createOrder, errorMessage, type StockShortage } from '@oism/shared';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Flex, Form, Input, InputNumber, message, Select } from 'antd';
import { useNavigate } from 'react-router-dom';
import { toFieldPath } from '../common/FormModal';
import { useBranchOptions } from '../inventory/useBranchOptions';
import { type PosSkuOption, PosSkuSelect } from './PosSkuSelect';

type LineValues = { sku: PosSkuOption; quantity: number; unitPrice?: number | null; discount?: number | null };

type Values = { branchId: string; note?: string; items: LineValues[] };

// UC-ORD-02: đơn thủ công cho khách đặt qua điện thoại hoặc tin nhắn. Tạo xong là đơn ở Reserved, hàng đã được giữ.
// Không nhập đơn giá thì backend lấy giá lẻ hiện tại của SKU.
export function NewOrderForm() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<Values>();
  const branchId = Form.useWatch('branchId', form);
  const branches = useBranchOptions();

  const create = useMutation({
    mutationFn: ({ items, ...rest }: Values) =>
      createOrder({
        ...rest,
        items: items.map(({ sku, quantity, unitPrice, discount }) => ({
          skuId: sku.value,
          quantity,
          unitPrice: unitPrice ?? undefined,
          discount: discount ?? undefined,
        })),
      }),
    onSuccess: async (order) => {
      void message.success(`Đã tạo đơn ${order.orderNumber} và giữ hàng`);
      // Giữ hàng làm đổi tồn khả dụng.
      await Promise.all(['orders', 'stock', 'pos-skus'].map((key) => queryClient.invalidateQueries({ queryKey: [key] })));
      navigate(`/orders/${order.id}`);
    },
    onError: (failure) => {
      if (failure instanceof ApiError && failure.code === 'validation_failed') {
        form.setFields(Object.entries(failure.errors).map(([name, errors]) => ({ name: toFieldPath(name) as never, errors })));
      }
      // Tồn khả dụng trong ô chọn SKU đã cũ.
      void queryClient.invalidateQueries({ queryKey: ['pos-skus'] });
    },
  });

  return (
    <Card style={{ maxWidth: 960 }}>
      {branches.error && <Alert type="error" showIcon title={errorMessage(branches.error)} style={{ marginBottom: 16 }} />}
      {create.error && (
        <Alert
          type="error"
          showIcon
          title={errorMessage(create.error)}
          description={shortageLines(create.error, form.getFieldValue('items') as LineValues[] | undefined)}
          style={{ marginBottom: 16 }}
        />
      )}
      <Form<Values> form={form} layout="vertical" onFinish={(values) => !create.isPending && create.mutate(values)}>
        <Form.Item name="branchId" label="Chi nhánh xuất hàng" rules={[{ required: true, message: 'Chọn chi nhánh' }]}>
          <Select options={branches.activeOptions} style={{ maxWidth: 360 }} />
        </Form.Item>
        {/* Đơn mới mở sẵn một dòng trống. */}
        <Form.List name="items" initialValue={[{ quantity: 1 }]}>
          {(fields, { add, remove }) => (
            <Flex vertical gap={8} style={{ marginBottom: 24 }}>
              {fields.map((field) => (
                <Flex key={field.key} gap={8} align="start" wrap>
                  <Form.Item
                    name={[field.name, 'sku']}
                    rules={[{ required: true, message: 'Chọn SKU' }]}
                    style={{ flex: 1, minWidth: 280, marginBottom: 0 }}
                  >
                    <PosSkuSelect branchId={branchId} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'quantity']} rules={[{ required: true, message: 'Nhập số lượng' }]} style={{ marginBottom: 0 }}>
                    <InputNumber min={1} precision={0} placeholder="Số lượng" aria-label="Số lượng" style={{ width: 110 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'unitPrice']} style={{ marginBottom: 0 }}>
                    <InputNumber min={0} placeholder="Giá lẻ hiện tại" aria-label="Đơn giá" suffix="₫" style={{ width: 170 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'discount']} style={{ marginBottom: 0 }}>
                    <InputNumber min={0} placeholder="Giảm giá" aria-label="Giảm giá của dòng" suffix="₫" style={{ width: 150 }} />
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
        <Form.Item name="note" label="Ghi chú">
          <Input.TextArea rows={2} maxLength={500} placeholder="Tên, số điện thoại, địa chỉ giao của khách" />
        </Form.Item>
        <Flex gap={8}>
          <Button type="primary" htmlType="submit" loading={create.isPending}>
            Tạo đơn và giữ hàng
          </Button>
          <Button onClick={() => navigate('/orders')}>Hủy</Button>
        </Flex>
      </Form>
    </Card>
  );
}

// 409 `insufficient_stock`: tên SKU và số còn lại lấy từ `details` của phản hồi (docs/design/ui/admin.md).
function shortageLines(error: unknown, items: LineValues[] = []) {
  if (!(error instanceof ApiError) || error.code !== 'insufficient_stock' || !Array.isArray(error.details)) return undefined;

  const labels = new Map(items.filter((item) => item?.sku).map((item) => [item.sku.value, item.sku.label]));
  return (
    <ul style={{ margin: 0, paddingLeft: 20 }}>
      {(error.details as StockShortage[]).map((shortage) => (
        <li key={shortage.skuId}>
          {labels.get(shortage.skuId) ?? shortage.skuId}: cần {shortage.requested}, còn {shortage.available}
        </li>
      ))}
    </ul>
  );
}
