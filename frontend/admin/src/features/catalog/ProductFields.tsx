import { Form, Input, Select, TreeSelect } from 'antd';
import { useCatalogOptions } from './useCatalogOptions';

export type ProductValues = { name: string; categoryId: string; brandId?: string; description?: string };

// Các ô chung của form tạo và form sửa sản phẩm.
export function ProductFields() {
  const { categoryTree, brandOptions } = useCatalogOptions();

  return (
    <>
      <Form.Item name="name" label="Tên sản phẩm" rules={[{ required: true, whitespace: true, message: 'Nhập tên sản phẩm' }]}>
        <Input autoFocus maxLength={200} />
      </Form.Item>
      <Form.Item name="categoryId" label="Danh mục" rules={[{ required: true, message: 'Chọn danh mục' }]}>
        <TreeSelect treeDefaultExpandAll placeholder="Chọn danh mục" treeData={categoryTree} />
      </Form.Item>
      <Form.Item name="brandId" label="Thương hiệu">
        <Select allowClear placeholder="Không có" options={brandOptions} />
      </Form.Item>
      <Form.Item name="description" label="Mô tả">
        <Input.TextArea rows={2} maxLength={2000} />
      </Form.Item>
    </>
  );
}
