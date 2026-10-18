import { Breadcrumb, Flex, Typography } from 'antd';
import type { ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { findEntry } from './menu';

type Props = {
  // Bỏ trống thì lấy tên mục menu của trang.
  title?: ReactNode;
  // Nhãn cuối của breadcrumb ở trang con, ví dụ mã đơn ở chi tiết đơn.
  crumb?: string;
  // Tag hay dòng phụ đứng cạnh tiêu đề.
  tags?: ReactNode;
  // Nút hành động bên phải.
  extra?: ReactNode;
};

// Đầu trang: breadcrumb theo nhóm menu, rồi tiêu đề trang (docs/design/ui/admin.md mục "Bố cục và phong cách").
export function PageHeader({ title, crumb, tags, extra }: Props) {
  const { pathname } = useLocation();
  const found = findEntry(pathname);
  const isSubPage = found !== undefined && found.entry.path !== pathname;
  const items = [
    ...(found && found.group.entries.length > 1 ? [{ title: found.group.label }] : []),
    ...(found
      ? [{ title: isSubPage ? <Link to={found.entry.path}>{found.entry.label}</Link> : found.entry.label }]
      : []),
    ...(isSubPage && crumb ? [{ title: crumb }] : []),
  ];

  return (
    <div className="no-print" style={{ marginBottom: 16 }}>
      {items.length > 1 && <Breadcrumb items={items} style={{ marginBottom: 8 }} />}
      <Flex justify="space-between" align="center" gap={16} wrap>
        <Flex align="center" gap={12} wrap>
          <Typography.Title level={3} style={{ margin: 0 }}>
            {title ?? found?.entry.label}
          </Typography.Title>
          {tags}
        </Flex>
        {extra && (
          <Flex gap={8} wrap>
            {extra}
          </Flex>
        )}
      </Flex>
    </div>
  );
}
