// T01 trên gói Compose, NFR-PERF-02: 50 đơn cùng lúc tranh 1 sản phẩm còn đúng 1 cái. Kiểm cả độ trễ lẫn tính đúng:
// đúng 1 đơn giữ được hàng, 49 đơn nhận 409 insufficient_stock, tồn không âm (docs/plans/tools-k6.md).
import { check, fail } from 'k6';
import exec from 'k6/execution';
import { Counter } from 'k6/metrics';
import { branchByCode, expect, get, login, post, put, skuByCode } from './lib.js';

const accepted = new Counter('flash_sale_accepted');
const refused = new Counter('flash_sale_refused');
const failed = new Counter('flash_sale_failed');

export const options = {
  scenarios: {
    // 50 người dùng ảo, mỗi người đúng một đơn, cùng bắt đầu.
    rush: { executor: 'per-vu-iterations', vus: 50, iterations: 1, maxDuration: '1m' },
  },
  thresholds: {
    flash_sale_accepted: ['count==1'],
    flash_sale_refused: ['count==49'],
    flash_sale_failed: ['count==0'],
    'http_req_duration{name:flash-order}': ['p(95)<2000'],
  },
};

// Hạt điều rang muối: seed không bán và không giữ SKU này, nên đặt tồn về 1 bằng kiểm kê không vướng đơn nào.
const CODE = 'HD-RM';

export function setup() {
  const headers = login('owner@xanh.test');
  const branch = branchByCode(headers, 'XANH-DD');
  const sku = skuByCode(headers, branch.id, CODE);

  // Kiểm kê đếm còn 1: tồn về đúng 1 qua bút toán điều chỉnh của sổ, không sửa số dư bằng tay.
  const stocktake = expect(post('/api/core/stocktakes', { branchId: branch.id }, headers, 'stocktake'), 201, 'open stocktake');
  expect(put(`/api/core/stocktakes/${stocktake.id}/counts`, { items: [{ skuId: sku.skuId, countedQty: 1 }] }, headers, 'stocktake'), 200, 'count');
  expect(post(`/api/core/stocktakes/${stocktake.id}/post`, undefined, headers, 'stocktake'), 200, 'post stocktake');
  const before = stockOf(headers, branch.id, sku.skuId);
  if (before.available !== 1) fail(`expected 1 available before the rush, got ${JSON.stringify(before)}`);
  return { headers, branchId: branch.id, skuId: sku.skuId };
}

export default function ({ headers, branchId, skuId }) {
  const response = post('/api/core/orders', { branchId, note: `Flash sale k6 #${exec.vu.idInTest}`, items: [{ skuId, quantity: 1 }] }, headers, 'flash-order');
  if (response.status === 201) accepted.add(1);
  else if (response.status === 409 && response.json('code') === 'insufficient_stock') refused.add(1);
  else failed.add(1);
  check(response, { 'flash 201 or 409': (r) => r.status === 201 || r.status === 409 });
}

// Kiểm số dư sau cùng rồi hủy đơn thắng để lần chạy sau đặt lại tồn được.
export function teardown({ headers, branchId, skuId }) {
  const after = stockOf(headers, branchId, skuId);
  check(after, { 'onHand 1, reserved 1, available 0': (s) => s.onHand === 1 && s.reserved === 1 && s.available === 0 });
  const orders = expect(get(`/api/core/orders?status=Reserved&branchId=${branchId}&pageSize=100`, headers, 'orders'), 200, 'list orders');
  for (const order of orders.items.filter((o) => (o.note || '').startsWith('Flash sale k6'))) {
    expect(post(`/api/core/orders/${order.id}/cancel`, undefined, headers, 'cancel'), 200, 'cancel winner');
  }
}

function stockOf(headers, branchId, skuId) {
  const page = expect(get(`/api/core/stock?branchId=${branchId}&skuId=${skuId}`, headers, 'stock'), 200, 'stock');
  return page.items[0];
}
