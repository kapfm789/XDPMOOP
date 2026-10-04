import { createProduct, errorMessage, listProducts, type ProductListItem, useAuth } from '@oism/shared';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Flex, Form, Input, Select, Switch, Table, Tag, TreeSelect } from 'antd';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { FormModal } from '../common/FormModal';
import { ProductFields, type ProductValues } from './ProductFields';
import { SkuFields, type SkuValues, toSkuRequest } from './SkuFields';
import { useCatalogOptions } from './useCatalogOptions';

type Values = ProductValues & { hasVariants: boolean; skus: SkuValues[] };

type Filter = { query?: string; categoryId?: string; brandId?: string; page: number };

const PAGE_SIZE = 20;

// UC-PROD-02: danh sách sản phẩm và form tạo sản phẩm đơn hoặc sản phẩm có biến thể.
// Mã SKU trùng, số SKU của sản phẩm đơn và quyền đặt giá do backend quyết định.
export function ProductsPanel() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isOwner = useAuth().user?.role === 'Owner';
  const [filter, setFilter] = useState<Filter>({ page: 1 });
  const [creating, setCreating] = useState(false);
  const options = useCatalogOptions();
  const products = useQuery({
    queryKey: ['products', filter],
    queryFn: () => listProducts({ ...filter, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });
  const error = products.error ?? options.error;
  // Đổi bộ lọc thì quay về trang đầu.
  const narrow = (change: Partial<Filter>) => setFilter((current) => ({ ...current, ...change, page: 1 }));

  return (
    <>
      <Flex justify="space-between" gap={16} wrap style={{ marginBottom: 16 }}>
        <Flex gap={8} wrap>
          <Input.Search
            allowClear
            placeholder="Tìm theo tên hoặc mã SKU"
            aria-label="Tìm sản phẩm"
            onSearch={(query) => narrow({ query })}
            style={{ width: 280 }}
          />
          <TreeSelect
            allowClear
            treeDefaultExpandAll
            placeholder="Danh mục"
            treeData={options.categoryTree}
            value={filter.categoryId}
            onChange={(categoryId?: string) => narrow({ categoryId })}
            style={{ width: 200 }}
          />
          <Select
            allowClear
            placeholder="Thương hiệu"
            options={options.brandOptions}
            value={filter.brandId}
            onChange={(brandId?: string) => narrow({ brandId })}
            style={{ width: 200 }}
          />
        </Flex>
        <Button type="primary" onClick={() => setCreating(true)}>
          Thêm sản phẩm
        </Button>
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<ProductListItem>
        rowKey="id"
        loading={products.isPending}
        dataSource={products.data?.items}
        pagination={{
          current: filter.page,
          pageSize: PAGE_SIZE,
          total: products.data?.total,
          showSizeChanger: false,
          onChange: (page) => setFilter((current) => ({ ...current, page })),
        }}
        columns={[
          {
            title: 'Tên sản phẩm',
            dataIndex: 'name',
            render: (name: string, product) => <Link to={`/catalog/products/${product.id}`}>{name}</Link>,
          },
          { title: 'Danh mục', dataIndex: 'categoryId', render: (id: string) => options.categoryName(id) },
          { title: 'Thương hiệu', dataIndex: 'brandId', render: (id?: string | null) => options.brandName(id) },
          {
            title: 'Loại',
            dataIndex: 'hasVariants',
            render: (hasVariants: boolean) => (hasVariants ? 'Có biến thể' : 'Sản phẩm đơn'),
          },
          { title: 'Số SKU', dataIndex: 'skuCount', align: 'right' },
          {
            title: 'Trạng thái',
            dataIndex: 'isActive',
            render: (isActive: boolean) => (isActive ? <Tag color="green">Đang bán</Tag> : <Tag>Ngừng bán</Tag>),
          },
        ]}
      />
      {creating && (
        <FormModal<Values>
          title="Thêm sản phẩm"
          width={640}
          initialValues={{ hasVariants: false, skus: [{ skuCode: '' }] }}
          submit={async ({ hasVariants, skus, ...product }) => {
            const created = await createProduct({
              ...product,
              hasVariants,
              // Sản phẩm đơn có đúng một SKU và không có thuộc tính.
              skus: hasVariants ? skus.map(toSkuRequest) : [toSkuRequest({ ...skus[0], attributes: [] })],
            });
            await queryClient.invalidateQueries({ queryKey: ['products'] });
            // Mã vạch được thêm ở trang chi tiết, sau khi SKU đã có.
            navigate(`/catalog/products/${created.id}`);
          }}
          onClose={() => setCreating(false)}
        >
          <ProductFields />
          <Form.Item name="hasVariants" label="Có biến thể (màu, size...)" valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item noStyle dependencies={['hasVariants']}>
            {({ getFieldValue }) => {
              const hasVariants = getFieldValue('hasVariants') as boolean;
              return (
                <Form.List name="skus">
                  {(fields, { add, remove }) => (
                    <Flex vertical gap={12}>
                      {fields.slice(0, hasVariants ? undefined : 1).map((field, index) => (
                        <Card
                          key={field.key}
                          size="small"
                          title={hasVariants ? `Biến thể ${index + 1}` : 'SKU'}
                          extra={
                            fields.length > 1 && (
                              <Button type="link" danger onClick={() => remove(field.name)}>
                                Xóa
                              </Button>
                            )
                          }
                        >
                          <SkuFields path={[field.name]} code attributes={hasVariants} prices={isOwner ? 'optional' : undefined} />
                        </Card>
                      ))}
                      {hasVariants && (
                        <Button type="dashed" onClick={() => add({ skuCode: '' })}>
                          Thêm biến thể
                        </Button>
                      )}
                    </Flex>
                  )}
                </Form.List>
              );
            }}
          </Form.Item>
        </FormModal>
      )}
    </>
  );
}
