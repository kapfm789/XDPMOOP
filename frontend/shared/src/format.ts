const VND = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' });

const VIETNAM_TIME = new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

// Tiền theo định dạng Việt Nam, không có phần thập phân, ví dụ `150.000 ₫` (docs/conventions/frontend.md).
export const formatMoney = (amount: number) => VND.format(amount);

// Thời gian từ API là UTC; hiển thị theo giờ Việt Nam, ví dụ `10:15 20/10/2026`.
export const formatDateTime = (iso: string) => VIETNAM_TIME.format(new Date(iso));
