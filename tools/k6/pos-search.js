// T17, NFR-PERF-01: tìm SKU ở POS theo tên, mã SKU và mã vạch, p95 dưới 200 ms (docs/testing/scenarios.md).
// Thu ngân của Tạp hóa Xanh tìm ở chi nhánh của mình, như gõ hoặc quét ở ô tìm của POS.
import { check } from 'k6';
import { expect, get, login } from './lib.js';

export const options = {
  scenarios: {
    search: { executor: 'constant-vus', vus: 20, duration: '1m' },
  },
  thresholds: {
    'http_req_duration{name:pos-search}': ['p(95)<200'],
    'checks{check:search 200}': ['rate>0.99'],
  },
};

// Từ khóa có trong dữ liệu seed: phần tên, phần mã SKU.
const TERMS = ['nước', 'sữa', 'mì', 'bánh', 'NS-500', 'MT-CC', 'trà', 'dầu', 'kẹo', 'gạo', 'SN-KT', 'bột'];

export function setup() {
  const headers = login('cashier@xanh.test');
  const me = expect(get('/api/identity/me', headers, 'me'), 200, 'me');
  return { headers, branchId: me.branchId };
}

export default function ({ headers, branchId }) {
  const term = TERMS[Math.floor(Math.random() * TERMS.length)];
  const response = get(`/api/core/pos/skus?branchId=${branchId}&query=${encodeURIComponent(term)}`, headers, 'pos-search');
  check(response, { 'search 200': (r) => r.status === 200 });
}
