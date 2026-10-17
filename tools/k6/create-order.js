// T17, NFR-PERF-01: tạo đơn thủ công và POS checkout, p95 dưới 500 ms (docs/testing/scenarios.md).
// Owner của Tạp hóa Xanh bán ở chi nhánh XANH-CG: 10 quầy POS và 5 người tạo đơn thủ công, mỗi người nghỉ 1 giây giữa hai
// đơn như thao tác thật. Bước chuẩn bị nhập thêm một lô lớn cho vài SKU để lần đo không cạn hàng.
// Đặt K6_THINK=0 để đo mức bão hòa: không nghỉ, mọi đơn tranh cùng vài dòng khóa số dư (docs/testing/load-test.md).
import { check, sleep } from 'k6';
import { branchByCode, expect, get, login, post, skuByCode, uuid } from './lib.js';

export const options = {
  scenarios: {
    checkout: { executor: 'constant-vus', vus: 10, duration: '1m', exec: 'checkout' },
    order: { executor: 'constant-vus', vus: 5, duration: '1m', exec: 'order' },
  },
  thresholds: {
    'http_req_duration{name:pos-checkout}': ['p(95)<500'],
    'http_req_duration{name:create-order}': ['p(95)<500'],
    'checks{check:checkout 201}': ['rate>0.99'],
    'checks{check:order 201}': ['rate>0.99'],
  },
};

const THINK = Number(__ENV.K6_THINK ?? 1);

const CODES = ['NS-500-C500', 'ST-KD-H180', 'TX-450', 'MT-CC-GOI', 'SN-KT-TN', 'SU-CH-KD'];

export function setup() {
  const headers = login('owner@xanh.test');
  const branch = branchByCode(headers, 'XANH-CG');
  const skus = CODES.map((code) => skuByCode(headers, branch.id, code));

  // Lô nhập riêng cho lần đo: tồn và giá vốn đi đúng đường phiếu nhập, không sửa số dư bằng tay.
  const suppliers = expect(get('/api/core/suppliers', headers, 'suppliers'), 200, 'list suppliers');
  const receipt = expect(
    post('/api/core/purchase-receipts', {
      branchId: branch.id,
      supplierId: suppliers[0].id,
      note: 'Lô cho đo tải k6',
      items: skus.map((sku) => ({ skuId: sku.skuId, quantity: 20000, unitCost: Math.round(sku.retailPrice * 0.7) })),
    }, headers, 'receipt'),
    201,
    'create receipt',
  );
  expect(post(`/api/core/purchase-receipts/${receipt.id}/confirm`, undefined, headers, 'receipt'), 200, 'confirm receipt');
  return { headers, branchId: branch.id, skus };
}

function cart(skus) {
  const first = Math.floor(Math.random() * skus.length);
  return [skus[first], skus[(first + 1) % skus.length]];
}

export function checkout({ headers, branchId, skus }) {
  const lines = cart(skus);
  const amount = lines.reduce((sum, sku) => sum + sku.retailPrice, 0);
  const response = post(
    '/api/core/pos/checkout',
    { branchId, items: lines.map((sku) => ({ skuId: sku.skuId, quantity: 1 })), payment: { method: 'Cash', amount } },
    Object.assign({ 'Idempotency-Key': uuid() }, headers),
    'pos-checkout',
  );
  check(response, { 'checkout 201': (r) => r.status === 201 });
  sleep(THINK);
}

export function order({ headers, branchId, skus }) {
  const response = post(
    '/api/core/orders',
    { branchId, note: 'Đơn đo tải k6', items: cart(skus).map((sku) => ({ skuId: sku.skuId, quantity: 1 })) },
    headers,
    'create-order',
  );
  check(response, { 'order 201': (r) => r.status === 201 });
  sleep(THINK);
}
