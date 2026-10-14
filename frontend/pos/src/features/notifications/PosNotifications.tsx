import { formatMoney, playChime, useNotifications } from '@oism/shared';
import { notification } from 'antd';
import { usePosBranch } from '../branch/usePosBranch';

// UC-SIM-02: đơn mới của chi nhánh đang bán hiện popup kèm âm thanh (docs/design/ui/pos.md). Popup không nhận focus nên
// ô tìm vẫn giữ con trỏ để quét tiếp. Cashier chỉ nhận đơn của chi nhánh mình từ server; Owner nhận của mọi chi nhánh
// nên lọc theo chi nhánh đang chọn ở POS.
export function PosNotifications() {
  // Nằm dưới thanh tiêu đề để không che nút đăng xuất, và xa thanh thanh toán ở đáy màn hình cầm tay.
  const [api, holder] = notification.useNotification({ placement: 'topRight', top: 72 });
  const branch = usePosBranch();

  useNotifications({
    onOrderCreated: (order) => {
      if (branch.branchId && order.branchId !== branch.branchId) return;
      playChime();
      api.info({
        key: `order-${order.orderId}`,
        title: `Đơn mới ${order.orderNumber}`,
        description: `${order.channel} · ${formatMoney(order.totalAmount)}`,
      });
    },
  });

  return holder;
}
