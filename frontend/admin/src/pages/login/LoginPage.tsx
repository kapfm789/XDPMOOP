import { LoginForm, useAuth } from '@oism/shared';
import { Typography } from 'antd';
import { Link, Navigate, useLocation } from 'react-router-dom';
import { AuthCard } from '../../features/auth/AuthCard';

export function LoginPage() {
  const { user } = useAuth();
  const from = (useLocation().state as { from?: string } | null)?.from ?? '/';

  // Đăng nhập xong thì phiên có user và trang tự chuyển đi.
  if (user) return <Navigate to={from} replace />;

  return (
    <AuthCard title="Đăng nhập quản trị" subtitle="Dành cho chủ cửa hàng và nhân viên" width={420}>
      <LoginForm />
      <Typography.Paragraph style={{ marginTop: 16, marginBottom: 0, textAlign: 'center' }}>
        Chưa có cửa hàng? <Link to="/register">Đăng ký</Link>
      </Typography.Paragraph>
    </AuthCard>
  );
}
