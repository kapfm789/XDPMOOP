import { formatDateTime, type Order } from '@oism/shared';
import { CHANNEL_LABELS } from './labels';

const CELL = { border: '1px solid #000', padding: '4px 8px' } as const;

// UC-ORD-06 AC-3: phiếu giao hàng gồm mã đơn, kênh, chi nhánh, danh sách hàng và số lượng. Vùng này chỉ hiện khi in
// (lớp print-only ở index.html); phiếu giao không có giá.
export function DeliverySlip({ order, branchName }: { order: Order; branchName: string }) {
  return (
    <div className="print-only" style={{ color: '#000', fontSize: 14 }}>
      <h2 style={{ textAlign: 'center', marginTop: 0 }}>PHIẾU GIAO HÀNG</h2>
      <p>
        Mã đơn: <strong>{order.orderNumber}</strong>
        {order.externalOrderId && ` (mã của sàn: ${order.externalOrderId})`}
      </p>
      <p>Kênh: {CHANNEL_LABELS[order.channel]}</p>
      <p>Chi nhánh xuất hàng: {branchName}</p>
      <p>Ngày duyệt: {order.confirmedAt ? formatDateTime(order.confirmedAt) : ''}</p>
      {order.note && <p>Ghi chú: {order.note}</p>}
      <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: 12 }}>
        <thead>
          <tr>
            <th style={{ ...CELL, textAlign: 'left' }}>Mã SKU</th>
            <th style={{ ...CELL, textAlign: 'left' }}>Tên hàng</th>
            <th style={{ ...CELL, textAlign: 'right' }}>Số lượng</th>
          </tr>
        </thead>
        <tbody>
          {order.items.map((item) => (
            <tr key={item.id}>
              <td style={CELL}>{item.skuCode}</td>
              <td style={CELL}>{item.skuName}</td>
              <td style={{ ...CELL, textAlign: 'right' }}>{item.quantity}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p style={{ textAlign: 'right' }}>Tổng số lượng: {order.items.reduce((sum, item) => sum + item.quantity, 0)}</p>
    </div>
  );
}
