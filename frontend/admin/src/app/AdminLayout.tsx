import { getMe, useAuth } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';
import { Avatar, Button, Divider, Flex, Layout, Menu, type MenuProps, Tag, Tooltip, Typography } from 'antd';
import { useEffect, useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { NotificationCenter } from '../features/notifications/NotificationCenter';
import { Icon, Logo } from '../features/common/icons';
import { findEntry, menuFor } from './menu';
import { COLORS } from './theme';

const ROLE_LABELS = { Owner: 'Chủ cửa hàng', Staff: 'Nhân viên', Cashier: 'Thu ngân' };
const ROLE_COLORS = { Owner: 'blue', Staff: 'geekblue', Cashier: 'default' };

// Thiết kế cho màn hình từ 1180 px; hẹp hơn thì thanh bên tự thu gọn còn icon (docs/design/ui/admin.md).
const NARROW = '(max-width: 1179px)';

function useNarrowScreen() {
  const [narrow, setNarrow] = useState(() => window.matchMedia(NARROW).matches);
  useEffect(() => {
    const query = window.matchMedia(NARROW);
    const onChange = (event: MediaQueryListEvent) => setNarrow(event.matches);
    query.addEventListener('change', onChange);
    return () => query.removeEventListener('change', onChange);
  }, []);
  return narrow;
}

// Chữ cái đầu của tên gọi, là từ cuối của họ tên tiếng Việt.
const initialOf = (fullName: string) => fullName.trim().split(/\s+/).pop()?.charAt(0).toUpperCase() ?? '';

export function AdminLayout() {
  const { user, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const narrow = useNarrowScreen();
  const [collapsed, setCollapsed] = useState(narrow);
  const current = findEntry(location.pathname);
  const [openKeys, setOpenKeys] = useState<string[]>(current ? [current.group.key] : []);
  // Tên cửa hàng chỉ có ở /me, không có trong phản hồi đăng nhập.
  const me = useQuery({ queryKey: ['me'], queryFn: getMe, staleTime: Infinity });

  useEffect(() => setCollapsed(narrow), [narrow]);

  // Mở sẵn nhóm của trang đang xem, kể cả khi tới trang đó bằng cách bấm vào thông báo.
  const currentGroup = current?.group.key;
  useEffect(() => {
    if (currentGroup) setOpenKeys((keys) => (keys.includes(currentGroup) ? keys : [...keys, currentGroup]));
  }, [currentGroup]);

  const items: MenuProps['items'] = menuFor(user?.role).map((group) =>
    group.entries.length === 1
      ? { key: group.entries[0].path, icon: <Icon name={group.icon} />, label: group.entries[0].label }
      : {
          key: group.key,
          icon: <Icon name={group.icon} />,
          label: group.label,
          children: group.entries.map((entry) => ({ key: entry.path, label: entry.label })),
        },
  );

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Layout.Sider
        width={240}
        collapsedWidth={72}
        collapsed={collapsed}
        trigger={null}
        style={{ borderRight: `1px solid ${COLORS.border}`, position: 'sticky', top: 0, height: '100vh', overflow: 'auto' }}
      >
        <Flex
          align="center"
          justify={collapsed ? 'center' : 'flex-start'}
          style={{ height: 64, paddingInline: collapsed ? 0 : 20, borderBottom: `1px solid ${COLORS.border}` }}
        >
          <Logo withText={!collapsed} />
        </Flex>
        <Menu
          mode="inline"
          style={{ borderInlineEnd: 'none', padding: 8 }}
          selectedKeys={current ? [current.entry.path] : []}
          {...(collapsed ? {} : { openKeys, onOpenChange: setOpenKeys })}
          items={items}
          onClick={({ key }) => navigate(key)}
        />
      </Layout.Sider>
      <Layout>
        <Layout.Header
          style={{ borderBottom: `1px solid ${COLORS.border}`, position: 'sticky', top: 0, zIndex: 10, lineHeight: 'normal' }}
        >
          <Flex justify="space-between" align="center" gap={16} style={{ height: '100%' }}>
            <Flex align="center" gap={12} style={{ minWidth: 0 }}>
              <Button
                type="text"
                aria-label={collapsed ? 'Mở rộng thanh bên' : 'Thu gọn thanh bên'}
                icon={<Icon name={collapsed ? 'unfold' : 'fold'} />}
                onClick={() => setCollapsed((value) => !value)}
              />
              <Typography.Text strong ellipsis style={{ fontSize: 16 }}>
                {me.data?.tenantName}
              </Typography.Text>
            </Flex>
            <Flex align="center" gap={12}>
              <NotificationCenter />
              <Divider orientation="vertical" style={{ height: 28 }} />
              {user && (
                <Flex align="center" gap={10}>
                  <Avatar style={{ background: COLORS.primary, flex: 'none' }}>{initialOf(user.fullName)}</Avatar>
                  <Flex vertical gap={2} style={{ lineHeight: 1.2 }}>
                    <Typography.Text strong>{user.fullName}</Typography.Text>
                    <span>
                      <Tag color={ROLE_COLORS[user.role]} style={{ marginInlineEnd: 0 }}>
                        {ROLE_LABELS[user.role]}
                      </Tag>
                    </span>
                  </Flex>
                </Flex>
              )}
              <Tooltip title="Đăng xuất">
                <Button type="text" aria-label="Đăng xuất" icon={<Icon name="logout" />} onClick={() => void logout()} />
              </Tooltip>
            </Flex>
          </Flex>
        </Layout.Header>
        <Layout.Content style={{ padding: 24 }}>
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  );
}
