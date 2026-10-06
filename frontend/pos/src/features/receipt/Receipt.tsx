import { formatDateTime, formatMoney, type Order } from '@oism/shared';
import { Divider, Flex, Typography } from 'antd';

const METHOD_LABELS = { Cash: 'Tiền mặt', QR: 'Chuyển khoản QR' } as const;

// Hóa đơn của một đơn đã lưu: mã đơn, thời gian, danh sách hàng, đơn giá, tổng tiền, phương thức thanh toán (UC-POS-03 AC-1).
// Mọi con số lấy từ đơn backend trả về, không tính lại từ giỏ.
export function Receipt({ order }: { order: Order }) {
  return (
    <div style={{ width: 320, maxWidth: '100%', margin: '0 auto', padding: 16, background: '#fff', color: '#000' }}>
      <Typography.Title level={4} style={{ textAlign: 'center', marginTop: 0 }}>
        HÓA ĐƠN BÁN HÀNG
      </Typography.Title>
      <Flex justify="space-between">
        <span>Mã đơn</span>
        <strong>{order.orderNumber}</strong>
      </Flex>
      <Flex justify="space-between">
        <span>Thời gian</span>
        <span>{formatDateTime(order.completedAt ?? order.createdAt)}</span>
      </Flex>
      <Divider dashed style={{ margin: '8px 0' }} />
      {order.items.map((item) => (
        <div key={item.id} style={{ marginBottom: 6 }}>
          <div>{item.skuName}</div>
          <Flex justify="space-between">
            <span>
              {item.quantity} × {formatMoney(item.unitPrice)}
              {item.discount > 0 && ` − ${formatMoney(item.discount)}`}
            </span>
            <span>{formatMoney(item.quantity * item.unitPrice - item.discount)}</span>
          </Flex>
        </div>
      ))}
      <Divider dashed style={{ margin: '8px 0' }} />
      <Flex justify="space-between">
        <strong>Tổng tiền</strong>
        <strong>{formatMoney(order.totalAmount)}</strong>
      </Flex>
      {order.payment && (
        <Flex justify="space-between">
          <span>Thanh toán</span>
          <span>{METHOD_LABELS[order.payment.method]}</span>
        </Flex>
      )}
      <Typography.Paragraph style={{ textAlign: 'center', margin: '12px 0 0' }}>Cảm ơn quý khách!</Typography.Paragraph>
    </div>
  );
}
