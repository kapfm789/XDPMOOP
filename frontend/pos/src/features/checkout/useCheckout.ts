import { ApiError, type Order, posCheckout, type PosCheckoutRequest } from '@oism/shared';
import { useRef, useState } from 'react';

// Mỗi lần bấm thanh toán sinh một Idempotency-Key. Chừng nào chưa biết chắc kết quả (lỗi mạng, lỗi 5xx) thì lần thử lại
// dùng lại đúng khóa đó, để backend trả đơn đã tạo thay vì tạo đơn thứ hai (kịch bản T03).
export function useCheckout() {
  const key = useRef<string | null>(null);
  const busy = useRef(false);
  const [pending, setPending] = useState(false);

  // Trả undefined khi một lần thanh toán khác đang chờ: bấm đúp không gửi request thứ hai.
  async function checkout(request: PosCheckoutRequest): Promise<Order | undefined> {
    if (busy.current) return undefined;
    busy.current = true;
    key.current ??= crypto.randomUUID();
    setPending(true);
    try {
      const order = await posCheckout(request, key.current);
      key.current = null;
      return order;
    } catch (error) {
      // Backend đã từ chối dứt khoát (4xx): không gì được lưu, lần bấm sau là một lần thanh toán mới.
      if (error instanceof ApiError && error.status < 500) key.current = null;
      throw error;
    } finally {
      busy.current = false;
      setPending(false);
    }
  }

  return { checkout, pending };
}
