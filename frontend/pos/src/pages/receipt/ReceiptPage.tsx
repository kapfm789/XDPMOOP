import type { Order } from '@oism/shared';
import { Button, Card, Flex, Result } from 'antd';
import { useEffect, useRef } from 'react';
import { Navigate, useLocation, useNavigate, useParams } from 'react-router-dom';
import { Receipt } from '../../features/receipt/Receipt';

// UC-POS-03: hóa đơn của đơn vừa bán. Dữ liệu là đơn backend đã trả về lúc thanh toán; trang này không gọi API,
// nên in lỗi hay in lại không bao giờ tạo thêm đơn.
export function ReceiptPage() {
  const { orderId } = useParams();
  const order = (useLocation().state as { order?: Order } | null)?.order;

  // Mở thẳng đường dẫn mà không qua thanh toán thì không có đơn để in.
  if (!order || order.id !== orderId) return <Navigate to="/" replace />;
  return <PrintableReceipt order={order} />;
}

function PrintableReceipt({ order }: { order: Order }) {
  const navigate = useNavigate();
  const printed = useRef(false);
  const nextSale = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    // StrictMode chạy effect hai lần ở dev; hộp thoại in chỉ mở một lần.
    if (printed.current) return;
    printed.current = true;
    window.print();
    // Đóng hộp thoại in xong, Enter đưa thu ngân về màn hình bán hàng.
    nextSale.current?.focus();
  }, []);

  return (
    <Flex vertical align="center" gap={16} style={{ paddingTop: 8 }}>
      <Result
        className="no-print"
        status="success"
        title="Thanh toán xong"
        subTitle={`Đơn ${order.orderNumber} đã lưu. Hộp thoại in đã mở; in lỗi thì bấm In lại.`}
        style={{ padding: 0 }}
      />
      <Card className="receipt-card" style={{ boxShadow: '0 8px 24px rgba(23, 32, 51, 0.08)' }} styles={{ body: { padding: 8 } }}>
        <Receipt order={order} />
      </Card>
      <Flex gap={12} className="no-print">
        <Button onClick={() => window.print()}>In lại</Button>
        <Button ref={nextSale} type="primary" onClick={() => navigate('/', { replace: true })}>
          Đơn mới (Enter)
        </Button>
      </Flex>
    </Flex>
  );
}
