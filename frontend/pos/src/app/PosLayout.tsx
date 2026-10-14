import { useAuth, useOnline } from '@oism/shared';
import { Alert, Button, Flex, Layout, Typography } from 'antd';
import { Outlet } from 'react-router-dom';
import { PosNotifications } from '../features/notifications/PosNotifications';

export function PosLayout() {
  const { user, logout } = useAuth();
  const online = useOnline();

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <PosNotifications />
      <Layout.Header className="no-print" style={{ paddingInline: 16 }}>
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
      {/* Không bán offline (ADR-0007): mất mạng thì báo và khóa nút thanh toán; giỏ vẫn giữ nguyên. */}
      {!online && (
        <Alert className="no-print" type="warning" banner title="Mất kết nối mạng. Chưa thanh toán được; giỏ hàng vẫn giữ nguyên." />
      )}
      <Layout.Content style={{ padding: 16 }}>
        <Outlet />
      </Layout.Content>
    </Layout>
  );
}
