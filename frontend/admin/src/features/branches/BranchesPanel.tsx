import {
  bankName,
  type Branch,
  type BranchType,
  createBranch,
  errorMessage,
  listBranches,
  setBranchActive,
  updateBranch,
  VIETQR_BANKS,
} from '@oism/shared';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Form, Input, message, Select, Switch, Table } from 'antd';
import { useState } from 'react';
import { FormModal } from '../common/FormModal';

type Values = { code: string; name: string; type: BranchType; address?: string; bankBin?: string; bankAccount?: string };

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
      <Card
        title="Danh sách chi nhánh"
        extra={
          <Button type="primary" onClick={() => setEditing('new')}>
            Thêm chi nhánh
          </Button>
        }
      >
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
              title: 'Tài khoản nhận',
              render: (_, branch) => (branch.bankBin && branch.bankAccount ? `${bankName(branch.bankBin)} · ${branch.bankAccount}` : null),
            },
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
      </Card>
      {editing && (
        <FormModal<Values>
          title={editing === 'new' ? 'Thêm chi nhánh' : `Sửa chi nhánh ${editing.code}`}
          initialValues={
            editing === 'new'
              ? { type: 'Store' }
              : {
                  ...editing,
                  address: editing.address ?? undefined,
                  bankBin: editing.bankBin ?? undefined,
                  bankAccount: editing.bankAccount ?? undefined,
                }
          }
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
          {/* Tài khoản nhận chuyển khoản cho mã VietQR ở POS: chọn cả ngân hàng và số tài khoản, hoặc bỏ trống cả hai. */}
          <Form.Item
            name="bankBin"
            label="Ngân hàng nhận chuyển khoản"
            dependencies={['bankAccount']}
            rules={[
              ({ getFieldValue }) => ({
                validator: (_, value?: string) =>
                  value || !getFieldValue('bankAccount')?.trim() ? Promise.resolve() : Promise.reject(new Error('Chọn ngân hàng')),
              }),
            ]}
          >
            <Select
              allowClear
              showSearch={{ optionFilterProp: 'label' }}
              placeholder="Không nhận chuyển khoản"
              options={VIETQR_BANKS.map((bank) => ({ value: bank.bin, label: `${bank.shortName} · ${bank.name}` }))}
            />
          </Form.Item>
          <Form.Item
            name="bankAccount"
            label="Số tài khoản nhận"
            dependencies={['bankBin']}
            rules={[
              { pattern: /^\s*[0-9A-Za-z]{1,19}\s*$/, message: 'Chỉ gồm chữ và số, tối đa 19 ký tự' },
              ({ getFieldValue }) => ({
                validator: (_, value?: string) =>
                  value?.trim() || !getFieldValue('bankBin') ? Promise.resolve() : Promise.reject(new Error('Nhập số tài khoản')),
              }),
            ]}
          >
            <Input maxLength={19} />
          </Form.Item>
        </FormModal>
      )}
    </>
  );
}
