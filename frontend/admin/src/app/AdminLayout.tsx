import { useAuth, visibleFor } from '@oism/shared';
import { Button, Flex, Layout, Menu, Typography } from 'antd';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { MENU } from './menu';

const ROLE_LABELS = { Owner: 'Chủ cửa hàng', Staff: 'Nhân viên', Cashier: 'Thu ngân' };

export function AdminLayout() {
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Layout.Sider breakpoint="lg" collapsedWidth={0}>
        <Typography.Title level={4} style={{ color: '#fff', margin: 16 }}>
          OISM
        </Typography.Title>
        <Menu
          theme="dark"
          selectedKeys={[location.pathname]}
          items={visibleFor(MENU, user?.role).map((entry) => ({ key: entry.path, label: entry.label }))}
          onClick={({ key }) => navigate(key)}
        />
      </Layout.Sider>
      <Layout>
        <Layout.Header style={{ background: '#fff', paddingInline: 24 }}>
          <Flex justify="end" align="center" gap={16} style={{ height: '100%' }}>
            {user && (
              <Typography.Text>
                {user.fullName} ({ROLE_LABELS[user.role]})
              </Typography.Text>
            )}
            <Button onClick={() => void logout()}>Đăng xuất</Button>
          </Flex>
        </Layout.Header>
        <Layout.Content style={{ padding: 24 }}>
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  );
}
