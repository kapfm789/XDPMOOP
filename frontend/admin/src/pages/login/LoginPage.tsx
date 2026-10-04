import { LoginForm, useAuth } from '@oism/shared';
import { Card, Flex, Typography } from 'antd';
import { Link, Navigate, useLocation } from 'react-router-dom';

export function LoginPage() {
  const { user } = useAuth();
  const from = (useLocation().state as { from?: string } | null)?.from ?? '/';

  // Đăng nhập xong thì phiên có user và trang tự chuyển đi.
  if (user) return <Navigate to={from} replace />;

  return (
    <Flex justify="center" align="center" style={{ minHeight: '100vh', padding: 16, boxSizing: 'border-box' }}>
      <Card title="OISM Quản trị" style={{ width: 400 }}>
        <LoginForm />
        <Typography.Paragraph style={{ marginTop: 16, marginBottom: 0, textAlign: 'center' }}>
          Chưa có cửa hàng? <Link to="/register">Đăng ký</Link>
        </Typography.Paragraph>
      </Card>
    </Flex>
  );
}
