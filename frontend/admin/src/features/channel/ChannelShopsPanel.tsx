import {
  type ChannelShop,
  createChannelShop,
  errorMessage,
  listChannelShops,
  type SalesChannel,
  setChannelShopActive,
  simulateBurst,
} from '@oism/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Flex, Form, Input, InputNumber, message, Modal, Select, Switch, Table, Typography } from 'antd';
import { useState } from 'react';
import { FormModal } from '../common/FormModal';
import { useBranchOptions } from '../inventory/useBranchOptions';

type ShopValues = { channel: SalesChannel; shopId: string; branchId: string };

type BurstValues = { skuCode: string; quantityPerOrder: number; orders: number; concurrency: number; unitPrice?: number | null };

export const SALES_CHANNEL_OPTIONS = (['Shopee', 'TikTok', 'Lazada'] as const).map((value) => ({ value, label: value }));

// UC-SIM-01: shop của sàn và công cụ bắn tải. Shop ánh xạ webhook của sàn sang tenant và chi nhánh xuất hàng;
// bắn tải nhờ backend tự sinh, tự ký và tự gửi webhook vào đúng endpoint của sàn.
export function ChannelShopsPanel() {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const [created, setCreated] = useState<ChannelShop | null>(null);
  const [bursting, setBursting] = useState<ChannelShop | null>(null);
  const branches = useBranchOptions();
  const shops = useQuery({ queryKey: ['channel-shops'], queryFn: listChannelShops });
  const error = shops.error ?? branches.error;
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['channel-shops'] });

  const toggle = useMutation({
    mutationFn: (shop: ChannelShop) => setChannelShopActive(shop.id, !shop.isActive),
    onSuccess: refresh,
    onError: (failure) => void message.error(errorMessage(failure)),
  });

  return (
    <>
      <Flex justify="end" style={{ marginBottom: 16 }}>
        <Button type="primary" onClick={() => setAdding(true)}>
          Thêm shop
        </Button>
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<ChannelShop>
        scroll={{ x: 'max-content' }}
        rowKey="id"
        loading={shops.isPending}
        dataSource={shops.data}
        pagination={false}
        columns={[
          { title: 'Kênh', dataIndex: 'channel' },
          { title: 'Mã shop', dataIndex: 'shopId' },
          { title: 'Chi nhánh xuất hàng', dataIndex: 'branchId', render: (id: string) => branches.name(id) },
          {
            title: 'Hoạt động',
            render: (_, shop) => (
              <Switch
                checked={shop.isActive}
                loading={toggle.isPending && toggle.variables.id === shop.id}
                onChange={() => toggle.mutate(shop)}
                aria-label={`Bật hoặc tắt shop ${shop.shopId}`}
              />
            ),
          },
          {
            render: (_, shop) => (
              // Shop đã tắt không nhận webhook nên không bắn tải được.
              <Button type="link" disabled={!shop.isActive} onClick={() => setBursting(shop)}>
                Bắn đơn giả lập
              </Button>
            ),
          },
        ]}
      />
      {adding && (
        <FormModal<ShopValues>
          title="Thêm shop của sàn"
          initialValues={{ channel: 'Shopee' }}
          submit={async (values) => {
            setCreated(await createChannelShop(values));
            await refresh();
          }}
          onClose={() => setAdding(false)}
        >
          <Form.Item name="channel" label="Kênh" rules={[{ required: true }]}>
            <Select options={SALES_CHANNEL_OPTIONS} />
          </Form.Item>
          <Form.Item name="shopId" label="Mã shop trên sàn" rules={[{ required: true, whitespace: true, message: 'Nhập mã shop' }]}>
            <Input autoFocus maxLength={100} placeholder="Ví dụ shopee-shop-01" />
          </Form.Item>
          <Form.Item name="branchId" label="Chi nhánh xuất hàng" rules={[{ required: true, message: 'Chọn chi nhánh' }]}>
            <Select options={branches.activeOptions} />
          </Form.Item>
        </FormModal>
      )}
      {created && (
        <Modal open title={`Đã tạo shop ${created.shopId}`} footer={<Button type="primary" onClick={() => setCreated(null)}>Đã lưu</Button>} onCancel={() => setCreated(null)}>
          <Typography.Paragraph>
            Khóa ký webhook của shop chỉ hiện một lần này. Bên gửi webhook dùng nó để tính header <code>X-Signature</code>; công cụ bắn đơn giả
            lập trên màn hình này thì không cần.
          </Typography.Paragraph>
          <Typography.Paragraph copyable code style={{ wordBreak: 'break-all' }}>
            {created.secret}
          </Typography.Paragraph>
        </Modal>
      )}
      {bursting && (
        <FormModal<BurstValues>
          title={`Bắn đơn giả lập từ ${bursting.channel} · ${bursting.shopId}`}
          initialValues={{ quantityPerOrder: 1, orders: 1, concurrency: 1 }}
          submit={async ({ unitPrice, ...values }) => {
            const result = await simulateBurst({ ...values, channel: bursting.channel, shopId: bursting.shopId, unitPrice: unitPrice ?? undefined });
            void message.success(`Đã gửi ${result.sent} webhook; kết quả giữ hàng hiện ở bảng webhook bên dưới`);
            await queryClient.invalidateQueries({ queryKey: ['webhook-events'] });
          }}
          onClose={() => setBursting(null)}
        >
          <Form.Item name="skuCode" label="Mã SKU" rules={[{ required: true, whitespace: true, message: 'Nhập mã SKU' }]}>
            <Input autoFocus maxLength={64} />
          </Form.Item>
          <Form.Item name="unitPrice" label="Đơn giá" extra="Bỏ trống thì đơn có đơn giá 0: kênh sàn không biết giá của SKU.">
            <InputNumber min={0} suffix="₫" style={{ width: '100%' }} />
          </Form.Item>
          <Flex gap={16}>
            <Form.Item name="quantityPerOrder" label="Số lượng mỗi đơn" rules={[{ required: true, message: 'Nhập số lượng' }]} style={{ flex: 1 }}>
              <InputNumber min={1} precision={0} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="orders" label="Số đơn" rules={[{ required: true, message: 'Nhập số đơn' }]} style={{ flex: 1 }}>
              <InputNumber min={1} max={1000} precision={0} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item name="concurrency" label="Gửi cùng lúc" rules={[{ required: true, message: 'Nhập số webhook gửi cùng lúc' }]} style={{ flex: 1 }}>
              <InputNumber min={1} max={100} precision={0} style={{ width: '100%' }} />
            </Form.Item>
          </Flex>
        </FormModal>
      )}
    </>
  );
}
