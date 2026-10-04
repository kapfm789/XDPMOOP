import { LoginForm, useAuth } from '@oism/shared';
import { Card, Flex } from 'antd';
import { Navigate } from 'react-router-dom';

export function LoginPage() {
  const { user } = useAuth();

  // Đăng nhập xong thì phiên có user và trang tự chuyển sang màn hình bán hàng.
  if (user) return <Navigate to="/" replace />;

  return (
    <Flex justify="center" align="center" style={{ minHeight: '100vh', padding: 16, boxSizing: 'border-box' }}>
      <Card title="OISM POS" style={{ width: 400 }}>
        <LoginForm />
      </Card>
    </Flex>
  );
}
