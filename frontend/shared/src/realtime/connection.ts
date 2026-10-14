import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';
import { GATEWAY_URL, refreshSession } from '../api/client';
import { getAccessToken } from '../auth/session';

// Hub thông báo của insights, đi qua gateway: docs/design/api/insights.md mục "Thông báo realtime".
const HUB_PATH = '/hubs/notifications';
const RETRY_DELAYS_MS = [0, 2_000, 5_000, 10_000];
const MAX_RETRY_DELAY_MS = 30_000;
const TOKEN_MARGIN_MS = 30_000;

// UC-SIM-02 AC-4: mất kết nối thì nối lại mãi, giãn dần tới 30 giây một lần.
export function retryDelay(previousRetryCount: number): number {
  return RETRY_DELAYS_MS[previousRetryCount] ?? MAX_RETRY_DELAY_MS;
}

// Access token sống 60 phút; kết nối lại sau đó cần token mới. Token đọc không được thì coi như đã hết hạn.
export function expiresWithin(token: string, marginMs: number, now = Date.now()): boolean {
  try {
    const payload: unknown = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    const exp = typeof payload === 'object' && payload !== null && 'exp' in payload ? payload.exp : undefined;
    return typeof exp !== 'number' || exp * 1000 - now <= marginMs;
  } catch {
    return true;
  }
}

async function hubToken(): Promise<string> {
  const token = getAccessToken();
  if (token && !expiresWithin(token, TOKEN_MARGIN_MS)) return token;
  await refreshSession();
  return getAccessToken() ?? '';
}

// Server tự đưa kết nối vào group theo token; client chỉ nghe. Trình duyệt gửi token trên query của WebSocket.
// withCredentials: false vì phiên không dùng cookie, nhờ vậy CORS ở gateway không cần bật credentials.
export function createNotificationConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(GATEWAY_URL + HUB_PATH, { accessTokenFactory: hubToken, withCredentials: false })
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) => retryDelay(context.previousRetryCount) })
    .configureLogging(LogLevel.Warning)
    .build();
}
