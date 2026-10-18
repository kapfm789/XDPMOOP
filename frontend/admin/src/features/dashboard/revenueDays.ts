import type { GrossProfitRow } from '@oism/shared';

export type RevenueDay = { date: string; revenue: number };

// Báo cáo nhóm theo ngày chỉ có dòng cho ngày có bán; biểu đồ cần đủ `count` ngày liên tiếp từ `from`, ngày trống là 0.
export function fillDays(rows: readonly GrossProfitRow[], from: string, count: number): RevenueDay[] {
  const revenue = new Map(rows.map((row) => [row.key, row.revenue]));
  return Array.from({ length: count }, (_, index) => {
    const day = new Date(`${from}T00:00:00Z`);
    day.setUTCDate(day.getUTCDate() + index);
    const date = day.toISOString().slice(0, 10);
    return { date, revenue: revenue.get(date) ?? 0 };
  });
}

// Mức trên cùng của trục: số tròn nhỏ nhất không thấp hơn giá trị lớn nhất, để vạch chia đọc dễ.
export function axisMax(values: readonly number[]): number {
  const max = Math.max(0, ...values);
  if (max === 0) return 1;
  const step = 10 ** Math.floor(Math.log10(max));
  return Math.ceil(max / step) * step;
}
