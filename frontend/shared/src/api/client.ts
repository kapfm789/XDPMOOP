import { endSession, getAccessToken, getRefreshToken, startSession } from '../auth/session';
import type { LoginResponse } from '../types/identity';

// Mọi lời gọi đi qua gateway: docs/architecture/context-and-containers.md mục "Địa chỉ và cổng".
const GATEWAY_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:8080';
const RETRY_LIMIT = 3;
const RETRY_DELAY_MS = 1000;

// Lỗi theo ProblemDetails: docs/design/api/README.md mục "Lỗi". Giao diện xử lý theo `code`.
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    title: string,
    readonly errors: Record<string, string[]> = {},
    readonly details?: unknown,
  ) {
    super(title);
  }
}

type Options = {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  // Endpoint công khai (đăng ký, đăng nhập, làm mới token): không gắn token và không tự làm mới khi gặp 401.
  anonymous?: boolean;
};

export async function api<T>(path: string, options: Options = {}): Promise<T> {
  for (let retry = 0; ; retry++) {
    try {
      return await request<T>(path, options);
    } catch (error) {
      // SKU hoặc chi nhánh vừa tạo chưa tới service do event trễ: thử lại rồi mới báo lỗi.
      if (!(error instanceof ApiError) || error.code !== 'reference_not_ready' || retry === RETRY_LIMIT) throw error;
      await new Promise((resolve) => setTimeout(resolve, RETRY_DELAY_MS));
    }
  }
}

async function request<T>(path: string, options: Options): Promise<T> {
  let response = await send(path, options);
  if (response.status === 401 && !options.anonymous && (await refreshSession())) response = await send(path, options);
  return read<T>(response);
}

function send(path: string, { method = 'GET', body, anonymous }: Options) {
  const headers: Record<string, string> = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const token = getAccessToken();
  if (token && !anonymous) headers.Authorization = `Bearer ${token}`;
  return fetch(GATEWAY_URL + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
}

async function read<T>(response: Response): Promise<T> {
  const body: unknown = response.status === 204 ? undefined : await response.json().catch(() => undefined);
  if (response.ok) return body as T;

  const problem = (body ?? {}) as { code?: string; title?: string; errors?: Record<string, string[]>; details?: unknown };
  throw new ApiError(
    response.status,
    problem.code ?? 'unknown',
    problem.title ?? `Máy chủ trả lỗi ${response.status}`,
    problem.errors,
    problem.details,
  );
}

let refreshing: Promise<boolean> | null = null;

// Đổi refresh token lấy cặp token mới. Các request gặp 401 cùng lúc dùng chung một lần gọi:
// refresh token xoay vòng, gọi hai lần với cùng token thì lần sau bị coi là dùng lại và cả chuỗi bị thu hồi.
export function refreshSession(): Promise<boolean> {
  refreshing ??= acrossTabs(async () => {
    // Đọc sau khi đã giữ khóa: tab khác có thể vừa xoay vòng token.
    const refreshToken = getRefreshToken();
    if (!refreshToken) {
      endSession(false);
      return false;
    }

    try {
      const login = await read<LoginResponse>(
        await send('/api/identity/auth/refresh', { method: 'POST', body: { refreshToken }, anonymous: true }),
      );
      startSession(login);
      return true;
    } catch (error) {
      // Server từ chối thì refresh token đã hết dùng được; lỗi mạng thì giữ lại để lần sau thử tiếp.
      endSession(error instanceof ApiError);
      return false;
    } finally {
      refreshing = null;
    }
  });
  return refreshing;
}

// Các tab dùng chung refresh token trong localStorage, nên việc làm mới phải lần lượt giữa các tab (Web Locks).
function acrossTabs(run: () => Promise<boolean>): Promise<boolean> {
  return 'locks' in navigator ? navigator.locks.request('oism.refresh', run) : run();
}
