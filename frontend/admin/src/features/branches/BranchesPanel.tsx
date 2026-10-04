import {
  type Branch,
  type BranchType,
  createBranch,
  errorMessage,
  listBranches,
  setBranchActive,
  updateBranch,
} from '@oism/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Flex, Form, Input, message, Select, Switch, Table } from 'antd';
import { useState } from 'react';
import { FormModal } from '../common/FormModal';

type Values = { code: string; name: string; type: BranchType; address?: string };

export const BRANCH_TYPE_LABELS: Record<BranchType, string> = { Store: 'Cửa hàng', Warehouse: 'Kho' };

// UC-AUTH-04: khai báo, sửa, bật và tắt chi nhánh.
export function BranchesPanel() {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<Branch | 'new' | null>(null);
  const { data, isPending, error } = useQuery({ queryKey: ['branches'], queryFn: () => listBranches() });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['branches'] });

  const toggle = useMutation({
    mutationFn: (branch: Branch) => setBranchActive(branch.id, !branch.isActive),
    onSuccess: refresh,
    onError: (failure) => void message.error(errorMessage(failure)),
  });

  return (
    <>
      <Flex justify="end" style={{ marginBottom: 16 }}>
        <Button type="primary" onClick={() => setEditing('new')}>
          Thêm chi nhánh
        </Button>
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<Branch>
        rowKey="id"
        loading={isPending}
        dataSource={data}
        pagination={false}
        columns={[
          { title: 'Mã', dataIndex: 'code' },
          { title: 'Tên', dataIndex: 'name' },
          { title: 'Loại', dataIndex: 'type', render: (type: BranchType) => BRANCH_TYPE_LABELS[type] },
          { title: 'Địa chỉ', dataIndex: 'address' },
          {
            title: 'Hoạt động',
            render: (_, branch) => (
              <Switch
                checked={branch.isActive}
                loading={toggle.isPending && toggle.variables.id === branch.id}
                onChange={() => toggle.mutate(branch)}
                aria-label={`Bật hoặc tắt chi nhánh ${branch.code}`}
              />
            ),
          },
          { render: (_, branch) => <Button type="link" onClick={() => setEditing(branch)}>Sửa</Button> },
        ]}
      />
      {editing && (
        <FormModal<Values>
          title={editing === 'new' ? 'Thêm chi nhánh' : `Sửa chi nhánh ${editing.code}`}
          initialValues={editing === 'new' ? { type: 'Store' } : { ...editing, address: editing.address ?? undefined }}
          submit={async ({ code, ...rest }) => {
            await (editing === 'new' ? createBranch({ code, ...rest }) : updateBranch(editing.id, rest));
            await refresh();
          }}
          onClose={() => setEditing(null)}
        >
          <Form.Item name="code" label="Mã chi nhánh" rules={[{ required: true, whitespace: true, message: 'Nhập mã chi nhánh' }]}>
            <Input autoFocus disabled={editing !== 'new'} maxLength={50} />
          </Form.Item>
          <Form.Item name="name" label="Tên" rules={[{ required: true, whitespace: true, message: 'Nhập tên chi nhánh' }]}>
            <Input maxLength={200} />
          </Form.Item>
          <Form.Item name="type" label="Loại" rules={[{ required: true }]}>
            <Select options={Object.entries(BRANCH_TYPE_LABELS).map(([value, label]) => ({ value, label }))} />
          </Form.Item>
          <Form.Item name="address" label="Địa chỉ">
            <Input maxLength={500} />
          </Form.Item>
        </FormModal>
      )}
    </>
  );
}
