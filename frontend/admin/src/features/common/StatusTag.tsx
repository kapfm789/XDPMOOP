import { Tag, Typography, theme } from 'antd';
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

// Xanh lá là đủ hàng hoặc thành công, cam là sắp hết hoặc đang chờ, đỏ là hết hàng hoặc lỗi; xanh dương là đang xử lý
// (docs/design/ui/admin.md mục "Bố cục và phong cách").
export type Tone = 'success' | 'pending' | 'error' | 'info' | 'neutral';

const TAG_COLORS: Record<Tone, string> = {
  success: 'success',
  pending: 'warning',
  error: 'error',
  info: 'processing',
  neutral: 'default',
};

export function StatusTag({ tone, children }: { tone: Tone; children: ReactNode }) {
  return (
    <Tag color={TAG_COLORS[tone]} variant="outlined">
      {children}
    </Tag>
  );
}

// Mã chứng từ (đơn, phiếu nhập, phiếu chuyển) in đậm màu chính; có `to` thì là link tới chi tiết.
export function DocumentCode({ code, to }: { code: string; to?: string }) {
  const { token } = theme.useToken();
  const text = (
    <Typography.Text strong style={{ color: token.colorPrimary }}>
      {code}
    </Typography.Text>
  );
  return to ? <Link to={to}>{text}</Link> : text;
}
