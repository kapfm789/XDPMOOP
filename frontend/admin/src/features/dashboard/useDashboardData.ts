import { listLowStockAlerts, listOrders } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';

export const PENDING_LIMIT = 10;

// Thẻ đơn chờ duyệt và thẻ cảnh báo tồn cao bằng nhau; danh sách dài cuộn bên trong thẻ (docs/design/ui/admin.md).
export const LIST_CARD_HEIGHT = 380;
// Phần thân bảng còn lại sau tiêu đề thẻ, lề thẻ và dòng tiêu đề bảng.
export const LIST_SCROLL_HEIGHT = LIST_CARD_HEIGHT - 56 - 48 - 40;
export const LIST_CARD_STYLE = { height: LIST_CARD_HEIGHT, display: 'flex', flexDirection: 'column' } as const;
export const LIST_CARD_BODY_STYLE = { flex: 1, minHeight: 0, overflow: 'auto' } as const;

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
