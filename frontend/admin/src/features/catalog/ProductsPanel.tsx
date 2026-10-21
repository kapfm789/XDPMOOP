import { createProduct, errorMessage, listProducts, loadBarcodeReader, type ProductListItem, useAuth } from '@oism/shared';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Flex, Form, Input, message, Select, Switch, Table, TreeSelect } from 'antd';
import { type ChangeEvent, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { FilterBar } from '../common/FilterBar';
import { FormModal } from '../common/FormModal';
import { StatusTag } from '../common/StatusTag';
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
  const [searchText, setSearchText] = useState('');
  const [reading, setReading] = useState(false);
  const imageInput = useRef<HTMLInputElement>(null);

  // UC-PROD-03 AC-8: đọc mã vạch trong ảnh rồi tìm sản phẩm giữ mã đó qua chính ô tìm (docs/design/ui/admin.md mục "Hình mã vạch").
  async function findByImage(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    // Cho chọn lại đúng ảnh vừa chọn.
    event.target.value = '';
    if (!file) return;
    setReading(true);
    try {
      let code: string | undefined;
      try {
        code = await (await loadBarcodeReader()).readImage(file);
      } catch {
        return void message.error('Không đọc được ảnh này.');
      }
      if (!code) return void message.warning('Không tìm thấy mã vạch trong ảnh.');
      const found = await listProducts({ query: code, page: 1, pageSize: 2 });
      if (found.total === 1) navigate(`/catalog/products/${found.items[0].id}`);
      else if (found.total === 0) message.warning(`Không có sản phẩm nào giữ mã ${code}`);
      else {
        setSearchText(code);
        narrow({ query: code });
      }
    } catch (failure) {
      message.error(errorMessage(failure));
    } finally {
      setReading(false);
    }
  }

  return (
    <>
      <FilterBar>
        <Input.Search
          allowClear
          placeholder="Tìm theo tên, mã SKU hoặc mã vạch"
          aria-label="Tìm sản phẩm"
          value={searchText}
          onChange={(event) => setSearchText(event.target.value)}
          onSearch={(query) => narrow({ query })}
          style={{ width: 280 }}
        />
        <input ref={imageInput} type="file" accept="image/*" hidden onChange={(event) => void findByImage(event)} />
        <Button loading={reading} onClick={() => imageInput.current?.click()}>
          Tìm bằng ảnh mã vạch
        </Button>
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
      </FilterBar>
      <Card
        title="Danh sách sản phẩm"
        extra={
          <Button type="primary" onClick={() => setCreating(true)}>
            Thêm sản phẩm
          </Button>
        }
      >
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
              render: (name: string, product) => (
                <Link to={`/catalog/products/${product.id}`} style={{ fontWeight: 600 }}>
                  {name}
                </Link>
              ),
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
              render: (isActive: boolean) =>
                isActive ? <StatusTag tone="success">Đang bán</StatusTag> : <StatusTag tone="neutral">Ngừng bán</StatusTag>,
            },
          ]}
        />
      </Card>
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
