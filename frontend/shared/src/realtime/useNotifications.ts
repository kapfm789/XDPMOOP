import { type HubConnection, HubConnectionState } from '@microsoft/signalr';
import { useEffect, useRef } from 'react';
import { useAuth } from '../auth/AuthProvider';
import type { LowStockNotification, OrderCreatedNotification, RemoteScanNotification } from '../types/insights';
import { createNotificationConnection, retryDelay } from './connection';

export type NotificationHandlers = {
  onOrderCreated?: (notification: OrderCreatedNotification) => void;
  onLowStock?: (notification: LowStockNotification) => void;
};

// Máy quét từ xa (W5-15) dùng chính kết nối của useNotifications: mỗi ứng dụng chỉ có một kết nối, để mã gửi đi
// không quay lại chính máy gửi qua một kết nối thứ hai.
let active: HubConnection | undefined;
const scanListeners = new Set<(code: string) => void>();

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
    connection.on('RemoteScan', (scan: RemoteScanNotification) => scanListeners.forEach((listener) => listener(scan.code)));
    active = connection;

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
      if (active === connection) active = undefined;
      void connection.stop();
    };
  }, [userId]);
}

// Gửi mã sang các màn hình bán hàng khác của chính người dùng (hub method `SendScan`). Chưa nối được hub thì báo lỗi
// ngay: mã không được gửi bù.
export async function sendRemoteScan(code: string): Promise<void> {
  if (active?.state !== HubConnectionState.Connected) throw new Error('hub_disconnected');
  await active.invoke('SendScan', code);
}

// Nhận mã do phiên khác của cùng người dùng gửi tới (thông báo `RemoteScan`).
export function useRemoteScan(handler: (code: string) => void) {
  const latest = useRef(handler);
  latest.current = handler;

  useEffect(() => {
    const listener = (code: string) => latest.current(code);
    scanListeners.add(listener);
    return () => {
      scanListeners.delete(listener);
    };
  }, []);
}
