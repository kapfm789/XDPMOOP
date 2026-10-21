import { bankName, formatDateTime, formatMoney } from '@oism/shared';
import { Divider, Flex, QRCode, Typography } from 'antd';
import { type CartLine, cartTotal } from '../cart/cart';
import { vietQrPayload } from '../checkout/vietQr';

type Props = { lines: CartLine[]; branchName: string; bankBin: string; bankAccount: string };

// Phiếu thanh toán tạm tính kèm mã VietQR (UC-POS-03 AC-4): in từ giỏ, chưa có đơn nên không có mã đơn.
// Không phải hóa đơn; hóa đơn chỉ in sau khi backend trả đơn đã lưu.
export function PaymentSlip({ lines, branchName, bankBin, bankAccount }: Props) {
  const total = cartTotal(lines);
  return (
    <div style={{ width: 320, maxWidth: '100%', margin: '0 auto', padding: 16, background: '#fff', color: '#000' }}>
      <Typography.Title level={4} style={{ textAlign: 'center', margin: 0 }}>
        PHIẾU THANH TOÁN
      </Typography.Title>
      <Typography.Paragraph style={{ textAlign: 'center', margin: '0 0 8px' }}>Tạm tính, chưa thanh toán</Typography.Paragraph>
      <Flex justify="space-between">
        <span>Chi nhánh</span>
        <span>{branchName}</span>
      </Flex>
      <Flex justify="space-between">
        <span>Thời gian</span>
        <span>{formatDateTime(new Date().toISOString())}</span>
      </Flex>
      <Divider dashed style={{ margin: '8px 0' }} />
      {lines.map((line) => (
        <div key={line.skuId} style={{ marginBottom: 6 }}>
          <div>{line.name}</div>
          <Flex justify="space-between">
            <span>
              {line.quantity} × {formatMoney(line.unitPrice)}
            </span>
            <span>{formatMoney(line.quantity * line.unitPrice)}</span>
          </Flex>
        </div>
      ))}
      <Divider dashed style={{ margin: '8px 0' }} />
      <Flex justify="space-between">
        <strong>Tổng tiền</strong>
        <strong>{formatMoney(total)}</strong>
      </Flex>
      <Flex vertical align="center" gap={4} style={{ marginTop: 12, textAlign: 'center' }}>
        <QRCode type="svg" value={vietQrPayload(bankBin, bankAccount, total)} size={180} color="#000" bordered={false} />
        <span>
          {bankName(bankBin)} · {bankAccount}
        </span>
        <span>Quét mã bằng ứng dụng ngân hàng để chuyển khoản</span>
      </Flex>
    </div>
  );
}
