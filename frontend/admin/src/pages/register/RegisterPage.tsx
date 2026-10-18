import { useAuth } from '@oism/shared';
import { Typography } from 'antd';
import { Link, Navigate } from 'react-router-dom';
import { AuthCard } from '../../features/auth/AuthCard';
import { RegisterTenantForm } from '../../features/auth/RegisterTenantForm';

export function RegisterPage() {
  const { user } = useAuth();

  // Đăng ký xong form tự đăng nhập bằng tài khoản Owner vừa tạo.
  if (user) return <Navigate to="/" replace />;

  return (
    <AuthCard title="Đăng ký cửa hàng" subtitle="Tạo cửa hàng mới và tài khoản chủ cửa hàng" width={460}>
      <RegisterTenantForm />
      <Typography.Paragraph style={{ marginTop: 16, marginBottom: 0, textAlign: 'center' }}>
        Đã có tài khoản? <Link to="/login">Đăng nhập</Link>
      </Typography.Paragraph>
    </AuthCard>
  );
}
