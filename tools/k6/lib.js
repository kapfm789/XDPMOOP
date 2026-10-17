// Dùng chung cho các kịch bản k6 (W5-03): gọi gateway như frontend, đăng nhập bằng tài khoản seed (deploy/seed/README.md).
import http from 'k6/http';
import { check, fail } from 'k6';

export const GATEWAY = __ENV.K6_GATEWAY || 'http://localhost:8080';
const PASSWORD = __ENV.K6_PASSWORD || 'Demo@2026';

// Đăng nhập và trả header mang access token. Token sống 60 phút, đủ cho một lần chạy.
export function login(identifier) {
  const response = http.post(`${GATEWAY}/api/identity/auth/login`, JSON.stringify({ identifier, password: PASSWORD }), {
    headers: { 'Content-Type': 'application/json' },
    tags: { name: 'login' },
  });
  if (response.status !== 200) fail(`login ${identifier} failed: ${response.status} ${response.body}`);
  return { Authorization: `Bearer ${response.json('accessToken')}`, 'Content-Type': 'application/json' };
}

export function get(path, headers, name) {
  return http.get(`${GATEWAY}${path}`, { headers, tags: { name: name || path } });
}

export function post(path, body, headers, name) {
  return http.post(`${GATEWAY}${path}`, body === undefined ? null : JSON.stringify(body), { headers, tags: { name: name || path } });
}

export function put(path, body, headers, name) {
  return http.put(`${GATEWAY}${path}`, JSON.stringify(body), { headers, tags: { name: name || path } });
}

// Gọi và đòi đúng mã HTTP; sai thì dừng kịch bản, vì bước chuẩn bị hỏng thì số đo không còn ý nghĩa.
export function expect(response, status, what) {
  if (!check(response, { [`${what}: ${status}`]: (r) => r.status === status })) {
    fail(`${what} returned ${response.status}: ${response.body}`);
  }
  return response.status === 204 ? null : response.json();
}

export function branchByCode(headers, code) {
  const branch = expect(get('/api/identity/branches', headers, 'branches'), 200, 'list branches').find((b) => b.code === code);
  if (!branch) fail(`branch ${code} not found: run the seed first`);
  return branch;
}

// SKU theo mã, kèm tồn khả dụng tại chi nhánh, qua API tìm SKU của POS.
export function skuByCode(headers, branchId, code) {
  const found = expect(get(`/api/core/pos/skus?branchId=${branchId}&query=${code}`, headers, 'pos-skus'), 200, `search ${code}`);
  const sku = found.find((s) => s.skuCode === code);
  if (!sku) fail(`SKU ${code} not found at branch: run the seed first`);
  return sku;
}

// Ngày yyyy-MM-dd theo giờ Việt Nam, lùi offsetDays ngày.
export function vietnamDate(offsetDays) {
  const now = new Date(Date.now() + 7 * 3600 * 1000 + offsetDays * 86400 * 1000);
  return now.toISOString().slice(0, 10);
}

// uuid phiên bản 4, cho Idempotency-Key của POS.
export function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}
