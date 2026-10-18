import { LoginForm, useAuth } from '@oism/shared';
import { Card, Flex, Typography } from 'antd';
import { Navigate } from 'react-router-dom';
import { Logo } from '../../features/common/Logo';

// Thẻ ở giữa trang trên nền gradient nhạt, logo ở trên, như admin (docs/design/ui/pos.md mục "Bố cục và phong cách").
export function LoginPage() {
  const { user } = useAuth();

  // Đăng nhập xong thì phiên có user và trang tự chuyển sang màn hình bán hàng.
  if (user) return <Navigate to="/" replace />;

  return (
    <Flex
      vertical
      justify="center"
      align="center"
      gap={24}
      style={{
        minHeight: '100vh',
        padding: 16,
        boxSizing: 'border-box',
        background: 'linear-gradient(135deg, #eaf2ff 0%, #f5f7fa 45%, #eef8f3 100%)',
      }}
    >
      <Logo size={40} />
      <Card style={{ width: 400, maxWidth: '100%', boxShadow: '0 8px 24px rgba(23, 32, 51, 0.08)' }} styles={{ body: { padding: 32 } }}>
        <Typography.Title level={3} style={{ marginTop: 0, marginBottom: 4 }}>
          Bán hàng tại quầy
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 24 }}>
          Đăng nhập bằng tài khoản thu ngân hoặc chủ cửa hàng.
        </Typography.Paragraph>
        <LoginForm />
      </Card>
    </Flex>
  );
}
