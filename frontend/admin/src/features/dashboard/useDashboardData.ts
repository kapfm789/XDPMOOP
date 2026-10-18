import { listLowStockAlerts, listOrders } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';

export const PENDING_LIMIT = 10;

// Đơn đang giữ hàng chờ duyệt, dùng chung cho thẻ số liệu và danh sách ở trang tổng quan. Khóa truy vấn bắt đầu bằng
// 'orders' nên thông báo OrderCreated làm nó tải lại.
export const usePendingOrders = () =>
  useQuery({
    queryKey: ['orders', 'pending'],
    queryFn: () => listOrders({ status: 'Reserved', page: 1, pageSize: PENDING_LIMIT }),
  });

// UC-REP-03: SKU có tồn khả dụng bằng hoặc dưới ngưỡng. Thông báo LowStock làm danh sách tải lại ngay; SKU lên lại trên
// ngưỡng thì không có thông báo nào, nên danh sách cũng tự tải lại mỗi phút.
export const useLowStockAlerts = (branchId?: string) =>
  useQuery({
    queryKey: ['low-stock', branchId ?? 'all'],
    queryFn: () => listLowStockAlerts(branchId),
    refetchInterval: 60_000,
  });
