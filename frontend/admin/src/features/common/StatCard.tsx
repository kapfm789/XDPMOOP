import { Card, Flex, Skeleton, Typography, theme } from 'antd';
import type { ReactNode } from 'react';
import { Icon, type IconName } from './icons';
import type { Tone } from './StatusTag';

type Props = {
  label: string;
  value: ReactNode;
  hint?: ReactNode;
  icon: IconName;
  tone: Tone;
  loading?: boolean;
};

// Thẻ số liệu của trang tổng quan và dải tổng hợp trên bảng tồn.
export function StatCard({ label, value, hint, icon, tone, loading }: Props) {
  const { token } = theme.useToken();
  const colors: Record<Tone, [string, string]> = {
    success: [token.colorSuccess, token.colorSuccessBg],
    pending: [token.colorWarning, token.colorWarningBg],
    error: [token.colorError, token.colorErrorBg],
    info: [token.colorPrimary, token.colorPrimaryBg],
    neutral: [token.colorTextSecondary, token.colorFillTertiary],
  };
  const [color, background] = colors[tone];

  return (
    <Card style={{ height: '100%' }}>
      <Flex justify="space-between" align="flex-start" gap={16}>
        <Flex vertical gap={4} style={{ minWidth: 0 }}>
          <Typography.Text type="secondary">{label}</Typography.Text>
          {loading ? (
            <Skeleton.Input active size="small" />
          ) : (
            <Typography.Text strong style={{ fontSize: 26, lineHeight: 1.25 }}>
              {value}
            </Typography.Text>
          )}
          {hint && (
            <Typography.Text type="secondary" style={{ fontSize: 13 }}>
              {hint}
            </Typography.Text>
          )}
        </Flex>
        <Flex
          align="center"
          justify="center"
          style={{ width: 44, height: 44, borderRadius: 7, color, background, flex: 'none' }}
        >
          <Icon name={icon} size={22} />
        </Flex>
      </Flex>
    </Card>
  );
}
