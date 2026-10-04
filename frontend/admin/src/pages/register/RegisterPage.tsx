import { useAuth } from '@oism/shared';
import { Card, Flex, Typography } from 'antd';
import { Link, Navigate } from 'react-router-dom';
import { RegisterTenantForm } from '../../features/auth/RegisterTenantForm';

export function RegisterPage() {
  const { user } = useAuth();

  // Đăng ký xong form tự đăng nhập bằng tài khoản Owner vừa tạo.
  if (user) return <Navigate to="/" replace />;

  return (
    <Flex justify="center" align="center" style={{ minHeight: '100vh', padding: 16, boxSizing: 'border-box' }}>
      <Card title="Đăng ký cửa hàng" style={{ width: 440 }}>
        <RegisterTenantForm />
        <Typography.Paragraph style={{ marginTop: 16, marginBottom: 0, textAlign: 'center' }}>
          Đã có tài khoản? <Link to="/login">Đăng nhập</Link>
        </Typography.Paragraph>
      </Card>
    </Flex>
  );
}
