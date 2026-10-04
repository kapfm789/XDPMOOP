import {
  type CategoryNode,
  createCategory,
  deleteCategory,
  errorMessage,
  getCategoryTree,
  updateCategory,
} from '@oism/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Flex, Form, Input, message, Popconfirm, Table, TreeSelect } from 'antd';
import { useState } from 'react';
import { FormModal } from '../common/FormModal';

type Values = { name: string; parentId?: string };

// Đang tạo dưới một danh mục cha (hoặc ở gốc), hoặc đang sửa một danh mục.
type Editing = { parentId?: string } | CategoryNode;

// Bảng của Ant Design coi mảng `children` rỗng là có thể mở rộng: nút lá bỏ `children`.
type Row = Omit<CategoryNode, 'children'> & { children?: Row[] };

const toRows = (nodes: CategoryNode[]): Row[] =>
  nodes.map(({ children, ...category }) => ({ ...category, children: children.length ? toRows(children) : undefined }));

type Option = { value: string; title: string; children: Option[] };

const toOptions = (nodes: CategoryNode[]): Option[] =>
  nodes.map((node) => ({ value: node.id, title: node.name, children: toOptions(node.children) }));

const idsOf = (nodes: CategoryNode[]): string[] => nodes.flatMap((node) => [node.id, ...idsOf(node.children)]);

// UC-PROD-01: cây danh mục nhiều cấp. Vòng lặp cha con và danh mục còn dùng do backend từ chối.
export function CategoriesPanel() {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<Editing | null>(null);
  // Cây mở hết theo mặc định, kể cả danh mục con vừa thêm: chỉ ghi nhớ những nút người dùng đã thu gọn.
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set());
  const { data, isPending, error } = useQuery({ queryKey: ['categories'], queryFn: getCategoryTree });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['categories'] });

  const remove = useMutation({
    mutationFn: deleteCategory,
    onSuccess: refresh,
    onError: (failure) => void message.error(errorMessage(failure)),
  });

  return (
    <>
      <Flex justify="end" style={{ marginBottom: 16 }}>
        <Button type="primary" onClick={() => setEditing({})}>
          Thêm danh mục
        </Button>
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<Row>
        rowKey="id"
        loading={isPending}
        dataSource={data && toRows(data)}
        pagination={false}
        expandable={{
          expandedRowKeys: idsOf(data ?? []).filter((id) => !collapsed.has(id)),
          onExpand: (expanded, category) =>
            setCollapsed((current) => {
              const next = new Set(current);
              if (expanded) next.delete(category.id);
              else next.add(category.id);
              return next;
            }),
        }}
        columns={[
          { title: 'Tên danh mục', dataIndex: 'name' },
          {
            width: 280,
            render: (_, category) => (
              <Flex gap={8}>
                <Button type="link" onClick={() => setEditing({ parentId: category.id })}>
                  Thêm con
                </Button>
                <Button type="link" onClick={() => setEditing(category as CategoryNode)}>
                  Sửa
                </Button>
                <Popconfirm
                  title={`Xóa danh mục ${category.name}?`}
                  okText="Xóa"
                  cancelText="Hủy"
                  onConfirm={() => remove.mutate(category.id)}
                >
                  <Button type="link" danger>
                    Xóa
                  </Button>
                </Popconfirm>
              </Flex>
            ),
          },
        ]}
      />
      {editing && (
        <FormModal<Values>
          title={'id' in editing ? `Sửa danh mục ${editing.name}` : 'Thêm danh mục'}
          initialValues={{ name: 'id' in editing ? editing.name : undefined, parentId: editing.parentId ?? undefined }}
          submit={async ({ name, parentId }) => {
            const request = { name, parentId: parentId ?? null };
            await ('id' in editing ? updateCategory(editing.id, request) : createCategory(request));
            await refresh();
          }}
          onClose={() => setEditing(null)}
        >
          <Form.Item name="name" label="Tên danh mục" rules={[{ required: true, whitespace: true, message: 'Nhập tên danh mục' }]}>
            <Input autoFocus maxLength={200} />
          </Form.Item>
          <Form.Item name="parentId" label="Danh mục cha">
            <TreeSelect allowClear treeDefaultExpandAll placeholder="Không có (danh mục gốc)" treeData={toOptions(data ?? [])} />
          </Form.Item>
        </FormModal>
      )}
    </>
  );
}
