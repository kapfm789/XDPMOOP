import { Card, Flex } from 'antd';
import type { ReactNode } from 'react';

// Hàng bộ lọc nằm dưới đầu trang, trên các thẻ nội dung (docs/design/ui/admin.md mục "Bố cục và phong cách").
export function FilterBar({ children, extra }: { children: ReactNode; extra?: ReactNode }) {
  return (
    <Card size="small" style={{ marginBottom: 16 }} styles={{ body: { padding: 12 } }}>
      <Flex justify="space-between" align="center" gap={8} wrap>
        <Flex gap={8} wrap align="center">
          {children}
        </Flex>
        {extra && (
          <Flex gap={8} wrap>
            {extra}
          </Flex>
        )}
      </Flex>
    </Card>
  );
}
