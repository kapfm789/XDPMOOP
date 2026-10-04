import { Typography } from 'antd';
import { UsersPanel } from '../../features/users/UsersPanel';

export function UsersPage() {
  return (
    <>
      <Typography.Title level={3}>Người dùng</Typography.Title>
      <UsersPanel />
    </>
  );
}
