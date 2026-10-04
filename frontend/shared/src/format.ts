const VND = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' });

// Tiền theo định dạng Việt Nam, không có phần thập phân, ví dụ `150.000 ₫` (docs/conventions/frontend.md).
export const formatMoney = (amount: number) => VND.format(amount);
