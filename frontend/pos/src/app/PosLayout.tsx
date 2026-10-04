import { useAuth } from '@oism/shared';
import { Button, Flex, Layout, Typography } from 'antd';
import { Outlet } from 'react-router-dom';

export function PosLayout() {
  const { user, logout } = useAuth();

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Layout.Header style={{ paddingInline: 16 }}>
        <Flex justify="space-between" align="center" style={{ height: '100%' }}>
          <Typography.Title level={4} style={{ color: '#fff', margin: 0 }}>
            OISM POS
          </Typography.Title>
          <Flex align="center" gap={16}>
            <Typography.Text style={{ color: '#fff' }}>{user?.fullName}</Typography.Text>
            <Button onClick={() => void logout()}>Đăng xuất</Button>
          </Flex>
        </Flex>
      </Layout.Header>
      <Layout.Content style={{ padding: 16 }}>
        <Outlet />
      </Layout.Content>
    </Layout>
  );
}
