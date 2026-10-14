import { useEffect, useRef } from 'react';
import { useAuth } from '../auth/AuthProvider';
import type { LowStockNotification, OrderCreatedNotification } from '../types/insights';
import { createNotificationConnection, retryDelay } from './connection';

export type NotificationHandlers = {
  onOrderCreated?: (notification: OrderCreatedNotification) => void;
  onLowStock?: (notification: LowStockNotification) => void;
};

// Nghe hub thông báo trong suốt phiên đăng nhập; đăng xuất thì đóng kết nối. Gọi một lần ở layout của ứng dụng.
// Thông báo phát ra lúc đang mất kết nối không được gửi bù (UC-SIM-02 AC-4).
export function useNotifications(handlers: NotificationHandlers) {
  const { user } = useAuth();
  const latest = useRef(handlers);
  latest.current = handlers;
  const userId = user?.id;

  useEffect(() => {
    if (!userId) return;

    const connection = createNotificationConnection();
    connection.on('OrderCreated', (notification: OrderCreatedNotification) => latest.current.onOrderCreated?.(notification));
    connection.on('LowStock', (notification: LowStockNotification) => latest.current.onLowStock?.(notification));

    // Sau khi đã nối, withAutomaticReconnect lo việc nối lại. Lần nối đầu lỗi (mạng, gateway chưa chạy) thì tự thử lại.
    let stopped = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const start = async (attempt: number) => {
      try {
        await connection.start();
      } catch {
        if (!stopped) timer = setTimeout(() => void start(attempt + 1), retryDelay(attempt + 1));
      }
    };
    void start(0);

    return () => {
      stopped = true;
      clearTimeout(timer);
      void connection.stop();
    };
  }, [userId]);
}
