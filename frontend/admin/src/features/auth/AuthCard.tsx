import { Card, Flex, Typography } from 'antd';
import type { ReactNode } from 'react';
import { Logo } from '../common/icons';

// Đăng nhập và đăng ký: thẻ ở giữa trang trên nền gradient nhạt, logo OISM ở trên (docs/design/ui/admin.md).
export function AuthCard({ title, subtitle, width, children }: { title: string; subtitle: string; width: number; children: ReactNode }) {
  return (
    <Flex
      vertical
      justify="center"
      align="center"
      gap={24}
      style={{
        minHeight: '100vh',
        padding: 16,
        boxSizing: 'border-box',
        background: 'linear-gradient(135deg, #eaf2ff 0%, #f5f7fa 45%, #eef8f3 100%)',
      }}
    >
      <Logo size={40} />
      <Card style={{ width, maxWidth: '100%', boxShadow: '0 8px 24px rgba(23, 32, 51, 0.08)' }} styles={{ body: { padding: 32 } }}>
        <Typography.Title level={3} style={{ marginTop: 0, marginBottom: 4 }}>
          {title}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 24 }}>
          {subtitle}
        </Typography.Paragraph>
        {children}
      </Card>
    </Flex>
  );
}
