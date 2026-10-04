import { ApiError } from './client';

// Thông báo cho người dùng, chọn theo `code` chứ không theo chuỗi server trả.
const MESSAGES: Record<string, string> = {
  unauthenticated: 'Sai tài khoản hoặc mật khẩu',
  forbidden: 'Bạn không có quyền thực hiện thao tác này',
  duplicate: 'Dữ liệu đã tồn tại',
  validation_failed: 'Dữ liệu không hợp lệ',
};

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return MESSAGES[error.code] ?? error.message;
  return 'Không kết nối được máy chủ';
}
