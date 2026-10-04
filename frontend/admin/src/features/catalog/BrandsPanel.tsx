import { type Brand, createBrand, errorMessage, listBrands, updateBrand } from '@oism/shared';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Flex, Form, Input, Table } from 'antd';
import { useState } from 'react';
import { FormModal } from '../common/FormModal';

type Values = { name: string };

// UC-PROD-01: thương hiệu. Tên trùng trong tenant do backend từ chối (409).
export function BrandsPanel() {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<Brand | 'new' | null>(null);
  const { data, isPending, error } = useQuery({ queryKey: ['brands'], queryFn: listBrands });

  return (
    <>
      <Flex justify="end" style={{ marginBottom: 16 }}>
        <Button type="primary" onClick={() => setEditing('new')}>
          Thêm thương hiệu
        </Button>
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<Brand>
        rowKey="id"
        loading={isPending}
        dataSource={data}
        pagination={false}
        columns={[
          { title: 'Tên thương hiệu', dataIndex: 'name' },
          { width: 100, render: (_, brand) => <Button type="link" onClick={() => setEditing(brand)}>Sửa</Button> },
        ]}
      />
      {editing && (
        <FormModal<Values>
          title={editing === 'new' ? 'Thêm thương hiệu' : `Sửa thương hiệu ${editing.name}`}
          initialValues={editing === 'new' ? undefined : { name: editing.name }}
          submit={async ({ name }) => {
            await (editing === 'new' ? createBrand(name) : updateBrand(editing.id, name));
            await queryClient.invalidateQueries({ queryKey: ['brands'] });
          }}
          onClose={() => setEditing(null)}
        >
          <Form.Item name="name" label="Tên thương hiệu" rules={[{ required: true, whitespace: true, message: 'Nhập tên thương hiệu' }]}>
            <Input autoFocus maxLength={200} />
          </Form.Item>
        </FormModal>
      )}
    </>
  );
}
