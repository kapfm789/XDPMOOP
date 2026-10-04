import { Button, Result } from 'antd';
import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import type { Role } from '../types/identity';
import { useAuth } from './AuthProvider';
import { canAccess } from './roles';

// Chặn route theo vai trò: chưa đăng nhập thì về /login, sai vai trò thì báo không có quyền.
export function RequireRole({ roles, children }: { roles: readonly Role[]; children: ReactNode }) {
  const { user, logout } = useAuth();
  const location = useLocation();

  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;

  if (!canAccess(user.role, roles)) {
    return (
      <Result
        status="403"
        title="Không có quyền"
        subTitle="Tài khoản của bạn không được vào trang này."
        extra={<Button onClick={() => void logout()}>Đăng xuất</Button>}
      />
    );
  }

  return children;
}
