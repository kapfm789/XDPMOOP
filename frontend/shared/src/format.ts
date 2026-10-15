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

// Ngày `yyyy-MM-dd` theo giờ Việt Nam của một thời điểm, lùi hoặc tiến `offsetDays` ngày; dùng cho bộ lọc báo cáo.
export function vietnamDate(instant: Date, offsetDays = 0): string {
  // en-CA cho đúng dạng yyyy-MM-dd.
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(instant);
  const shifted = new Date(`${today}T00:00:00Z`);
  shifted.setUTCDate(shifted.getUTCDate() + offsetDays);
  return shifted.toISOString().slice(0, 10);
}

// Ngày `yyyy-MM-dd` của API sang dạng hiển thị `20/10/2026`.
export const formatDate = (date: string) => date.split('-').reverse().join('/');

// Số lượng theo định dạng Việt Nam, ví dụ `1.234,5` với một chữ số thập phân.
export const formatNumber = (value: number, maximumFractionDigits = 0) =>
  new Intl.NumberFormat('vi-VN', { maximumFractionDigits }).format(value);

// Tỉ lệ như WAPE 0,1234 thành `12,3%`.
export const formatPercent = (ratio: number) =>
  new Intl.NumberFormat('vi-VN', { style: 'percent', maximumFractionDigits: 1 }).format(ratio);
