import { Spin } from 'antd';
import { type ReactNode, useEffect, useSyncExternalStore } from 'react';
import { refreshSession } from '../api/client';
import { login, logout } from '../api/identity';
import { getSession, subscribe } from './session';

export function useAuth() {
  const session = useSyncExternalStore(subscribe, getSession);
  return { ...session, login, logout };
}

// Mở lại phiên từ refresh token đã lưu trước khi hiện bất kỳ màn hình nào.
export function AuthProvider({ children }: { children: ReactNode }) {
  const { ready } = useAuth();

  useEffect(() => {
    if (!getSession().ready) void refreshSession();
  }, []);

  return ready ? children : <Spin fullscreen />;
}
