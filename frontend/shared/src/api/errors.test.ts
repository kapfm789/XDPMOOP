import { expect, test } from 'vitest';
import { ApiError } from './client';
import { errorMessage } from './errors';

test('picks the user message by error code, not by the server title', () => {
  const error = new ApiError(409, 'insufficient_stock', 'Insufficient stock');
  expect(errorMessage(error)).toBe('Không đủ tồn khả dụng');
  expect(errorMessage(new ApiError(409, 'reference_not_ready', 'x'))).toBe(
    'Chi nhánh hoặc SKU vừa tạo chưa được đồng bộ tới kho; thử lại sau ít giây',
  );
  expect(errorMessage(new ApiError(403, 'forbidden', 'x'))).toBe('Bạn không có quyền thực hiện thao tác này');
});

test('falls back to the server title for a code the app does not know', () => {
  expect(errorMessage(new ApiError(400, 'something_new', 'Thông báo từ máy chủ'))).toBe('Thông báo từ máy chủ');
});

test('reports anything that is not an API error as a connection problem', () => {
  expect(errorMessage(new TypeError('Failed to fetch'))).toBe('Không kết nối được máy chủ');
  expect(errorMessage(undefined)).toBe('Không kết nối được máy chủ');
});
