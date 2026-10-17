// T17, NFR-PERF-01, UC-REP-01 AC-7: báo cáo doanh thu với ít nhất 100.000 dòng bán, p95 dưới 2 giây.
// Chạy sau prepare-sales.sql (README.md cùng thư mục): Tạp hóa Xanh có hơn 100.000 dòng sales_facts trong 60 ngày.
import { check } from 'k6';
import { get, login, vietnamDate } from './lib.js';

export const options = {
  scenarios: {
    report: { executor: 'constant-vus', vus: 5, duration: '1m' },
  },
  thresholds: {
    'http_req_duration{name:gross-profit}': ['p(95)<2000'],
    'checks{check:report 200}': ['rate>0.99'],
  },
};

// Owner xem 60 ngày theo đủ bốn chiều nhóm và không nhóm, như màn hình báo cáo trên admin.
const GROUPS = ['', 'day', 'branch', 'channel', 'sku'];

export function setup() {
  return { headers: login('owner@xanh.test'), from: vietnamDate(-60), to: vietnamDate(0) };
}

export default function ({ headers, from, to }) {
  const groupBy = GROUPS[Math.floor(Math.random() * GROUPS.length)];
  const query = `from=${from}&to=${to}${groupBy ? `&groupBy=${groupBy}` : ''}`;
  const response = get(`/api/insights/reports/gross-profit?${query}`, headers, 'gross-profit');
  check(response, { 'report 200': (r) => r.status === 200 });
}
