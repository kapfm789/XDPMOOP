import {
  type AssignableRole,
  createUser,
  errorMessage,
  listBranches,
  listUsers,
  type Role,
  updateUser,
  type UserAccount,
} from '@oism/shared';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Flex, Form, Input, Select, Switch, Table, Tag } from 'antd';
import { useState } from 'react';
import { FormModal } from '../common/FormModal';

type Values = {
  fullName: string;
  email?: string;
  phone?: string;
  password: string;
  role: AssignableRole;
  branchId?: string;
  isActive: boolean;
};

const PAGE_SIZE = 20;
const ROLE_LABELS: Record<Role, string> = { Owner: 'Chủ cửa hàng', Staff: 'Nhân viên', Cashier: 'Thu ngân' };
const ASSIGNABLE_ROLES: AssignableRole[] = ['Staff', 'Cashier'];

// UC-AUTH-03: Owner tạo và sửa tài khoản Staff, Cashier. Tài khoản Owner không sửa được ở đây.
export function UsersPanel() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<UserAccount | 'new' | null>(null);
  const users = useQuery({
    queryKey: ['users', page],
    queryFn: () => listUsers(page, PAGE_SIZE),
    placeholderData: keepPreviousData,
  });
  const branches = useQuery({ queryKey: ['branches'], queryFn: () => listBranches() });
  const branchName = (id?: string | null) => branches.data?.find((branch) => branch.id === id)?.name ?? '';
  const error = users.error ?? branches.error;

  return (
    <>
      <Flex justify="end" style={{ marginBottom: 16 }}>
        <Button type="primary" onClick={() => setEditing('new')}>
          Thêm người dùng
        </Button>
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<UserAccount>
        rowKey="id"
        loading={users.isPending}
        dataSource={users.data?.items}
        pagination={{ current: page, pageSize: PAGE_SIZE, total: users.data?.total, showSizeChanger: false, onChange: setPage }}
        columns={[
          { title: 'Họ tên', dataIndex: 'fullName' },
          { title: 'Email', dataIndex: 'email' },
          { title: 'Số điện thoại', dataIndex: 'phone' },
          { title: 'Vai trò', dataIndex: 'role', render: (role: Role) => ROLE_LABELS[role] },
          { title: 'Chi nhánh', dataIndex: 'branchId', render: (id?: string | null) => branchName(id) },
          {
            title: 'Trạng thái',
            dataIndex: 'isActive',
            render: (isActive: boolean) => (isActive ? <Tag color="green">Hoạt động</Tag> : <Tag>Đã khóa</Tag>),
          },
          {
            render: (_, user) =>
              user.role !== 'Owner' && <Button type="link" onClick={() => setEditing(user)}>Sửa</Button>,
          },
        ]}
      />
      {editing && (
        <FormModal<Values>
          title={editing === 'new' ? 'Thêm người dùng' : `Sửa ${editing.fullName}`}
          initialValues={
            editing === 'new'
              ? { role: 'Staff' }
              : {
                  fullName: editing.fullName,
                  role: editing.role as AssignableRole,
                  branchId: editing.branchId ?? undefined,
                  isActive: editing.isActive,
                }
          }
          submit={async ({ fullName, email, phone, password, role, branchId, isActive }) => {
            await (editing === 'new'
              ? createUser({ fullName, email: email?.trim() || undefined, phone: phone?.trim() || undefined, password, role, branchId })
              : updateUser(editing.id, { fullName, role, branchId: branchId ?? null, isActive }));
            await queryClient.invalidateQueries({ queryKey: ['users'] });
          }}
          onClose={() => setEditing(null)}
        >
          <Form.Item name="fullName" label="Họ tên" rules={[{ required: true, whitespace: true, message: 'Nhập họ tên' }]}>
            <Input autoFocus maxLength={200} />
          </Form.Item>
          {editing === 'new' && (
            <>
              <Form.Item name="email" label="Email" rules={[{ type: 'email', message: 'Email không hợp lệ' }]}>
                <Input autoComplete="off" />
              </Form.Item>
              <Form.Item
                name="phone"
                label="Số điện thoại"
                dependencies={['email']}
                rules={[
                  ({ getFieldValue }) => ({
                    validator: (_, phone?: string) =>
                      phone?.trim() || (getFieldValue('email') as string | undefined)?.trim()
                        ? Promise.resolve()
                        : Promise.reject(new Error('Nhập ít nhất email hoặc số điện thoại')),
                  }),
                ]}
              >
                <Input autoComplete="off" />
              </Form.Item>
              <Form.Item
                name="password"
                label="Mật khẩu"
                rules={[
                  { required: true, message: 'Nhập mật khẩu' },
                  { min: 8, message: 'Mật khẩu tối thiểu 8 ký tự' },
                ]}
              >
                <Input.Password autoComplete="new-password" />
              </Form.Item>
            </>
          )}
          <Form.Item name="role" label="Vai trò" rules={[{ required: true }]}>
            <Select options={ASSIGNABLE_ROLES.map((role) => ({ value: role, label: ROLE_LABELS[role] }))} />
          </Form.Item>
          <Form.Item
            name="branchId"
            label="Chi nhánh"
            dependencies={['role']}
            rules={[
              ({ getFieldValue }) => ({
                required: getFieldValue('role') === 'Cashier',
                message: 'Thu ngân phải gắn với một chi nhánh',
              }),
            ]}
          >
            <Select
              allowClear
              loading={branches.isPending}
              options={branches.data
                ?.filter((branch) => branch.isActive || branch.id === (editing !== 'new' ? editing.branchId : undefined))
                .map((branch) => ({ value: branch.id, label: `${branch.code} · ${branch.name}` }))}
            />
          </Form.Item>
          {editing !== 'new' && (
            <Form.Item name="isActive" label="Hoạt động" valuePropName="checked">
              <Switch />
            </Form.Item>
          )}
        </FormModal>
      )}
    </>
  );
}
