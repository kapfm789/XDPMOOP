import { errorMessage, formatDate, formatMoney, getGrossProfit, vietnamDate } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';
import { Alert, Card, Skeleton, Typography, theme } from 'antd';
import { Link } from 'react-router-dom';
import { axisMax, fillDays, type RevenueDay } from './revenueDays';

const DAYS = 7;
const WIDTH = 1000;
const HEIGHT = 280;
const PAD = { top: 24, right: 12, bottom: 32, left: 64 };

const compact = new Intl.NumberFormat('vi-VN', { notation: 'compact', maximumFractionDigits: 1 });

// UC-REP-01: doanh thu 7 ngày gần nhất tính cả hôm nay, theo giờ Việt Nam; chỉ Owner (docs/design/ui/admin.md).
// Vẽ bằng SVG vì dự án chỉ dùng Ant Design, không có thư viện biểu đồ.
export function RevenueChart() {
  const now = new Date();
  const from = vietnamDate(now, -(DAYS - 1));
  const to = vietnamDate(now);
  const report = useQuery({
    queryKey: ['gross-profit', 'dashboard', from, to],
    queryFn: () => getGrossProfit({ from, to, groupBy: 'day' }),
  });
  const days = fillDays(report.data?.rows ?? [], from, DAYS);

  return (
    <Card title="Doanh thu 7 ngày gần nhất" extra={<Link to="/reports/gross-profit">Xem báo cáo</Link>}>
      {report.error && <Alert type="error" showIcon title={errorMessage(report.error)} style={{ marginBottom: 16 }} />}
      {report.isPending ? (
        <Skeleton active paragraph={{ rows: 6 }} />
      ) : (
        <>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
            Tổng 7 ngày: <Typography.Text strong>{formatMoney(report.data?.totals.revenue ?? 0)}</Typography.Text>
          </Typography.Paragraph>
          <Bars days={days} />
        </>
      )}
    </Card>
  );
}

function Bars({ days }: { days: RevenueDay[] }) {
  const { token } = theme.useToken();
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
