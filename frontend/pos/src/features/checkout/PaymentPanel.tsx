import { formatMoney, type PaymentMethod } from '@oism/shared';
import { Button, Flex, QRCode, Segmented, Typography } from 'antd';

// ponytail: mã chuyển khoản của cửa hàng là một chuỗi cấu hình lúc build, chưa theo chuẩn VietQR và chưa theo chi nhánh;
// hệ thống không đối soát, thu ngân tự kiểm đã nhận tiền (ADR-0007). Cần VietQR thật thì thêm cấu hình tài khoản theo chi nhánh.
const STORE_QR: string = import.meta.env.VITE_POS_QR ?? 'OISM';

type Props = {
  method: PaymentMethod;
  onMethodChange: (method: PaymentMethod) => void;
  total: number;
  disabled: boolean;
  pending: boolean;
  // Gợi ý phím tắt chỉ có ích khi có bàn phím.
  showShortcuts: boolean;
  onPay: () => void;
};

// Chọn tiền mặt hoặc QR và nút thanh toán: docs/design/ui/pos.md. Thẻ bao ngoài do màn hình bán hàng quyết định.
export function PaymentPanel({ method, onMethodChange, total, disabled, pending, showShortcuts, onPay }: Props) {
  return (
    <Flex vertical gap={12}>
      <Segmented<PaymentMethod>
        block
        value={method}
        onChange={onMethodChange}
        options={[
          { value: 'Cash', label: 'Tiền mặt' },
          { value: 'QR', label: 'Chuyển khoản QR' },
        ]}
      />
      {method === 'QR' && total > 0 && (
        <Flex vertical align="center" gap={6}>
          <QRCode value={`${STORE_QR}|${total}`} size={160} bordered={false} />
          <Typography.Text strong>{formatMoney(total)}</Typography.Text>
          <Typography.Text type="secondary">Kiểm đã nhận tiền rồi mới bấm thanh toán</Typography.Text>
        </Flex>
      )}
      <Button
        type="primary"
        block
        disabled={disabled}
        loading={pending}
        onClick={onPay}
        style={{ height: 56, fontSize: 18, fontWeight: 600, boxShadow: disabled ? undefined : '0 4px 12px rgba(22, 119, 255, 0.25)' }}
      >
        Thanh toán {formatMoney(total)}
      </Button>
      {showShortcuts && (
        <Typography.Text type="secondary" style={{ fontSize: 12, textAlign: 'center' }}>
          Enter: thanh toán · F4: đổi tiền mặt/QR · F2: về ô tìm · Esc: xóa ô tìm
        </Typography.Text>
      )}
    </Flex>
  );
}
