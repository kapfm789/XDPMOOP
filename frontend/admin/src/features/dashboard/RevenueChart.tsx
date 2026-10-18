import { errorMessage, formatDate, formatMoney, getGrossProfit, vietnamDate } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Skeleton, Typography, theme } from 'antd';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { axisMax, fillDays, type RevenueDay } from './revenueDays';

const DAYS = 7;
const DEFAULT_WIDTH = 1000;
const DEFAULT_HEIGHT = 280;
const PAD = { top: 24, right: 12, bottom: 32, left: 64 };

const compact = new Intl.NumberFormat('vi-VN', { notation: 'compact', maximumFractionDigits: 1 });

// Kích thước thật của vùng vẽ, để biểu đồ giãn theo chiều cao cột bên cạnh mà chữ không bị kéo méo.
// Ref dạng callback vì vùng vẽ chỉ xuất hiện sau khi dữ liệu về (lúc đầu là khung chờ).
function useElementSize<T extends HTMLElement>() {
  const [element, ref] = useState<T | null>(null);
  const [size, setSize] = useState<{ width: number; height: number }>();
  useEffect(() => {
    if (!element) return;
    const observer = new ResizeObserver(([entry]) => {
      const { width, height } = entry.contentRect;
      setSize((current) =>
        current && Math.round(current.width) === Math.round(width) && Math.round(current.height) === Math.round(height) ? current : { width, height },
      );
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [element]);
  return { ref, size };
}

// UC-REP-01: doanh thu 7 ngày gần nhất tính cả hôm nay, theo giờ Việt Nam; chỉ Owner (docs/design/ui/admin.md).
// Vẽ bằng SVG vì dự án chỉ dùng Ant Design, không có thư viện biểu đồ.
// fill: thẻ kéo cao bằng ô chứa nó (trang tổng quan đặt biểu đồ cạnh cột đơn chờ duyệt và cảnh báo tồn).
export function RevenueChart({ fill = false }: { fill?: boolean }) {
  const area = useElementSize<HTMLDivElement>();
  const now = new Date();
  const from = vietnamDate(now, -(DAYS - 1));
  const to = vietnamDate(now);
  const report = useQuery({
    queryKey: ['gross-profit', 'dashboard', from, to],
    queryFn: () => getGrossProfit({ from, to, groupBy: 'day' }),
  });
  const days = fillDays(report.data?.rows ?? [], from, DAYS);

  return (
    <Card
      title="Doanh thu 7 ngày gần nhất"
      extra={<Link to="/reports/gross-profit">Xem báo cáo</Link>}
      style={fill ? { height: '100%', display: 'flex', flexDirection: 'column' } : undefined}
      styles={fill ? { body: { flex: 1, display: 'flex', flexDirection: 'column', minHeight: 0 } } : undefined}
    >
      {report.error && <Alert type="error" showIcon title={errorMessage(report.error)} style={{ marginBottom: 16 }} />}
      {report.isPending ? (
        <Skeleton active paragraph={{ rows: 6 }} />
      ) : (
        <>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
            Tổng 7 ngày: <Typography.Text strong>{formatMoney(report.data?.totals.revenue ?? 0)}</Typography.Text>
          </Typography.Paragraph>
          {fill ? (
            // Biểu đồ nằm tuyệt đối trong vùng vẽ, để chiều cao của nó không đẩy cột cao thêm mà chỉ theo cột bên cạnh.
            <div ref={area.ref} style={{ flex: 1, minHeight: DEFAULT_HEIGHT, position: 'relative' }}>
              <div style={{ position: 'absolute', inset: 0 }}>
                <Bars days={days} width={area.size?.width} height={area.size?.height} />
              </div>
            </div>
          ) : (
            <Bars days={days} />
          )}
        </>
      )}
    </Card>
  );
}

// Không truyền kích thước thì vẽ theo khung 1000 × 280 và co giãn theo chiều ngang.
function Bars({ days, width, height }: { days: RevenueDay[]; width?: number; height?: number }) {
  const { token } = theme.useToken();
  const sized = !!width && !!height;
  const WIDTH = sized ? width : DEFAULT_WIDTH;
  const HEIGHT = sized ? Math.max(height, DEFAULT_HEIGHT) : DEFAULT_HEIGHT;
  const max = axisMax(days.map((day) => day.revenue));
  const plotWidth = WIDTH - PAD.left - PAD.right;
  const plotHeight = HEIGHT - PAD.top - PAD.bottom;
  const slot = plotWidth / days.length;
  const barWidth = Math.min(48, slot * 0.55);
  const y = (value: number) => PAD.top + plotHeight * (1 - value / max);
  const ticks = [0, 0.25, 0.5, 0.75, 1].map((ratio) => ratio * max);

  return (
    <svg
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      width="100%"
      height={sized ? HEIGHT : undefined}
      role="img"
      aria-label="Biểu đồ cột doanh thu theo ngày"
      style={{ display: 'block', fontFamily: token.fontFamily }}
    >
      {ticks.map((tick) => (
        <g key={tick}>
          <line x1={PAD.left} x2={WIDTH - PAD.right} y1={y(tick)} y2={y(tick)} stroke={token.colorBorderSecondary} />
          <text x={PAD.left - 8} y={y(tick) + 4} textAnchor="end" fontSize={12} fill={token.colorTextSecondary}>
            {compact.format(tick)}
          </text>
        </g>
      ))}
      {days.map((day, index) => {
        const x = PAD.left + slot * index + (slot - barWidth) / 2;
        const top = y(day.revenue);
        const isToday = index === days.length - 1;
        return (
          <g key={day.date}>
            <title>{`${formatDate(day.date)}: ${formatMoney(day.revenue)}`}</title>
            <rect
              x={x}
              y={top}
              width={barWidth}
              height={PAD.top + plotHeight - top}
              rx={4}
              fill={isToday ? token.colorPrimary : token.colorPrimaryBorder}
            />
            {day.revenue > 0 && (
              <text x={x + barWidth / 2} y={top - 6} textAnchor="middle" fontSize={12} fill={token.colorText}>
                {compact.format(day.revenue)}
              </text>
            )}
            <text
              x={x + barWidth / 2}
              y={HEIGHT - 10}
              textAnchor="middle"
              fontSize={12}
              fill={isToday ? token.colorText : token.colorTextSecondary}
              fontWeight={isToday ? 600 : 400}
            >
              {isToday ? 'Hôm nay' : formatDate(day.date).slice(0, 5)}
            </text>
          </g>
        );
      })}
    </svg>
  );
}
