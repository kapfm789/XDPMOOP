import {
  addBarcode,
  addSku,
  ApiError,
  type BarcodeSymbology,
  errorMessage,
  formatMoney,
  getProduct,
  removeBarcode,
  setPrices,
  type SetPricesRequest,
  type Sku,
  updateProduct,
  updateSku,
  useAuth,
} from '@oism/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Descriptions, Flex, Form, Input, message, Popconfirm, Select, Spin, Switch, Table, Tag, Typography } from 'antd';
import { type ReactNode, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { FormModal } from '../common/FormModal';
import { StatusTag } from '../common/StatusTag';
import { ProductFields, type ProductValues } from './ProductFields';
import { type AttributePair, SkuFields, type SkuValues, toAttributes, toPairs, toSkuRequest } from './SkuFields';
import { useCatalogOptions } from './useCatalogOptions';

type Dialog =
  | { kind: 'product' | 'addSku' }
  | { kind: 'editSku' | 'prices' | 'barcode'; sku: Sku };

type BarcodeValues = { symbology: BarcodeSymbology; code?: string };

const SYMBOLOGY_LABELS: Record<BarcodeSymbology, string> = { EAN13: 'EAN-13', Code128: 'Code128' };

// UC-PROD-02, 03, 04: một sản phẩm cùng SKU, mã vạch và giá của nó. Ô giá chỉ Owner sửa được;
// mã trùng, số kiểm tra EAN-13 và quyền đặt giá do backend quyết định.
// Đầu trang (breadcrumb, tiêu đề) do màn hình dựng; panel đưa tên sản phẩm, tag trạng thái và nút sửa.
export function ProductDetailPanel({
  productId,
  header,
}: {
  productId: string;
  header: (props: { title: string; tags: ReactNode; extra: ReactNode }) => ReactNode;
}) {
  const queryClient = useQueryClient();
  const isOwner = useAuth().user?.role === 'Owner';
  const options = useCatalogOptions();
  const [dialog, setDialog] = useState<Dialog | null>(null);
  const { data: product, error } = useQuery({
    queryKey: ['product', productId],
    queryFn: () => getProduct(productId),
    retry: false,
  });
  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['product', productId] }),
      queryClient.invalidateQueries({ queryKey: ['products'] }),
    ]);

  const removeCode = useMutation({
    mutationFn: ({ skuId, barcodeId }: { skuId: string; barcodeId: string }) => removeBarcode(skuId, barcodeId),
    onSuccess: refresh,
    onError: (failure) => void message.error(errorMessage(failure)),
  });

  // Sản phẩm không có hoặc thuộc tenant khác: đưa về danh sách (docs/conventions/frontend.md).
  if (error instanceof ApiError && error.code === 'not_found') return <Navigate to="/catalog/products" replace />;
  if (error) return <Alert type="error" showIcon title={errorMessage(error)} />;
  if (!product) return <Spin />;

  const close = () => setDialog(null);

  return (
    <>
      {header({
        title: product.name,
        tags: <ActiveTag isActive={product.isActive} />,
        extra: <Button onClick={() => setDialog({ kind: 'product' })}>Sửa sản phẩm</Button>,
      })}
      <Card title="Thông tin sản phẩm" style={{ marginBottom: 16 }}>
        <Descriptions
          column={{ xs: 1, md: 2 }}
          items={[
            { key: 'category', label: 'Danh mục', children: options.categoryName(product.categoryId) },
            { key: 'brand', label: 'Thương hiệu', children: options.brandName(product.brandId) || 'Không có' },
            { key: 'type', label: 'Loại', children: product.hasVariants ? 'Có biến thể' : 'Sản phẩm đơn' },
            {
              key: 'status',
              label: 'Trạng thái',
              children: <ActiveTag isActive={product.isActive} />,
            },
            { key: 'description', label: 'Mô tả', span: 2, children: product.description || 'Không có' },
          ]}
        />
      </Card>
      <Card
        title="SKU, mã vạch và giá"
        extra={
          product.hasVariants && (
            <Button type="primary" onClick={() => setDialog({ kind: 'addSku' })}>
              Thêm SKU
            </Button>
          )
        }
      >
        <Table<Sku>
          rowKey="id"
          dataSource={product.skus}
          pagination={false}
          columns={[
            { title: 'Mã SKU', dataIndex: 'skuCode', render: (skuCode: string) => <Typography.Text strong>{skuCode}</Typography.Text> },
            { title: 'Tên hiển thị', dataIndex: 'name' },
            { title: 'Giá lẻ', dataIndex: 'retailPrice', align: 'right', render: formatMoney },
            { title: 'Giá sỉ', dataIndex: 'wholesalePrice', align: 'right', render: formatMoney },
            {
              title: 'Mã vạch',
              render: (_, sku) => (
                <Flex gap={4} wrap>
                  {sku.barcodes.map((barcode) => (
                    <Popconfirm
                      key={barcode.id}
                      title={`Xóa mã vạch ${barcode.code}?`}
                      okText="Xóa"
                      cancelText="Hủy"
                      onConfirm={() => removeCode.mutate({ skuId: sku.id, barcodeId: barcode.id })}
                    >
                      {/* Bấm dấu x mở hộp xác nhận thay vì gỡ thẻ ngay. */}
                      <Tag closable onClose={(event) => event.preventDefault()} style={{ cursor: 'pointer' }}>
                        {barcode.code} · {SYMBOLOGY_LABELS[barcode.symbology]}
                      </Tag>
                    </Popconfirm>
                  ))}
                </Flex>
              ),
            },
            {
              title: 'Trạng thái',
              dataIndex: 'isActive',
              render: (isActive: boolean) => <ActiveTag isActive={isActive} />,
            },
            {
              render: (_, sku) => (
                <Flex gap={8} wrap>
                  <Button type="link" onClick={() => setDialog({ kind: 'editSku', sku })}>
                    Sửa
                  </Button>
                  {isOwner && (
                    <Button type="link" onClick={() => setDialog({ kind: 'prices', sku })}>
                      Đặt giá
                    </Button>
                  )}
                  <Button type="link" onClick={() => setDialog({ kind: 'barcode', sku })}>
                    Thêm mã vạch
                  </Button>
                </Flex>
              ),
            },
          ]}
        />
      </Card>
      {dialog?.kind === 'product' && (
        <FormModal<ProductValues & { isActive: boolean }>
          title={`Sửa sản phẩm ${product.name}`}
          initialValues={{
            name: product.name,
            categoryId: product.categoryId,
            brandId: product.brandId ?? undefined,
            description: product.description ?? undefined,
            isActive: product.isActive,
          }}
          submit={async ({ brandId, description, ...rest }) => {
            await updateProduct(product.id, { ...rest, brandId: brandId ?? null, description: description ?? null });
            await refresh();
          }}
          onClose={close}
        >
          <ProductFields />
          <Form.Item name="isActive" label="Đang bán" valuePropName="checked" extra="Tắt là ngừng bán mọi SKU của sản phẩm.">
            <Switch />
          </Form.Item>
        </FormModal>
      )}
      {dialog?.kind === 'addSku' && (
        <FormModal<SkuValues>
          title="Thêm SKU"
          submit={async (values) => {
            await addSku(product.id, toSkuRequest(values));
            await refresh();
          }}
          onClose={close}
        >
          <SkuFields code attributes prices={isOwner ? 'optional' : undefined} />
        </FormModal>
      )}
      {dialog?.kind === 'editSku' && (
        <FormModal<{ attributes?: AttributePair[]; isActive: boolean }>
          title={`Sửa SKU ${dialog.sku.skuCode}`}
          initialValues={{ attributes: toPairs(dialog.sku.attributes), isActive: dialog.sku.isActive }}
          submit={async ({ attributes, isActive }) => {
            await updateSku(dialog.sku.id, { attributes: toAttributes(attributes), isActive });
            await refresh();
          }}
          onClose={close}
        >
          <SkuFields attributes={product.hasVariants} />
          <Form.Item name="isActive" label="Đang bán" valuePropName="checked">
            <Switch />
          </Form.Item>
        </FormModal>
      )}
      {dialog?.kind === 'prices' && (
        <FormModal<SetPricesRequest>
          title={`Đặt giá ${dialog.sku.skuCode}`}
          initialValues={{ retailPrice: dialog.sku.retailPrice, wholesalePrice: dialog.sku.wholesalePrice }}
          submit={async (prices) => {
            await setPrices(dialog.sku.id, prices);
            await refresh();
          }}
          onClose={close}
        >
          <SkuFields prices="required" />
        </FormModal>
      )}
      {dialog?.kind === 'barcode' && (
        <FormModal<BarcodeValues>
          title={`Thêm mã vạch cho ${dialog.sku.skuCode}`}
          initialValues={{ symbology: 'EAN13' }}
          submit={async ({ symbology, code }) => {
            await addBarcode(dialog.sku.id, { symbology, code: code?.trim() || undefined });
            await refresh();
          }}
          onClose={close}
        >
          <Form.Item name="symbology" label="Loại mã" rules={[{ required: true }]}>
            <Select options={Object.entries(SYMBOLOGY_LABELS).map(([value, label]) => ({ value, label }))} />
          </Form.Item>
          <Form.Item
            name="code"
            label="Mã vạch"
            dependencies={['symbology']}
            extra="Với EAN-13, để trống thì hệ thống tự sinh mã."
            rules={[
              ({ getFieldValue }) => ({
                required: getFieldValue('symbology') === 'Code128',
                whitespace: true,
                message: 'Code128 phải nhập mã',
              }),
            ]}
          >
            <Input autoFocus maxLength={48} />
          </Form.Item>
        </FormModal>
      )}
    </>
  );
}

function ActiveTag({ isActive }: { isActive: boolean }) {
  return isActive ? <StatusTag tone="success">Đang bán</StatusTag> : <StatusTag tone="neutral">Ngừng bán</StatusTag>;
}
