import { getMe, useAuth, useOnline } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';
import { Alert, Avatar, Badge, Button, Divider, Flex, Grid, Layout, Select, Typography } from 'antd';
import { Outlet } from 'react-router-dom';
import { PosBranchProvider, usePosBranch } from '../features/branch/usePosBranch';
import { PosNotifications } from '../features/notifications/PosNotifications';
import { Logo } from '../features/common/Logo';
import { COLORS } from './theme';

const ROLE_LABELS = { Owner: 'Chủ cửa hàng', Staff: 'Nhân viên', Cashier: 'Thu ngân' };

// Chữ cái đầu của tên gọi, là từ cuối của họ tên tiếng Việt.
const initialOf = (fullName: string) => fullName.trim().split(/\s+/).pop()?.charAt(0).toUpperCase() ?? '';

export function PosLayout() {
  return (
    <PosBranchProvider>
      <Layout style={{ minHeight: '100vh' }}>
        <PosNotifications />
        <PosHeader />
        <OfflineBanner />
        <Layout.Content style={{ padding: 16 }}>
          <Outlet />
        </Layout.Content>
      </Layout>
    </PosBranchProvider>
  );
}

// Thanh trên: logo, chi nhánh đang bán, trạng thái mạng, thu ngân, đăng xuất (docs/design/ui/pos.md).
function PosHeader() {
  const { user, logout } = useAuth();
  const online = useOnline();
  const branch = usePosBranch();
  const wide = Grid.useBreakpoint().md;
  // Tên cửa hàng chỉ có ở /me, không có trong phản hồi đăng nhập.
  const me = useQuery({ queryKey: ['me'], queryFn: getMe, staleTime: Infinity });

  return (
    <Layout.Header className="no-print" style={{ borderBottom: `1px solid ${COLORS.border}`, position: 'sticky', top: 0, zIndex: 20 }}>
      <Flex justify="space-between" align="center" gap={12} style={{ height: '100%' }}>
        <Flex align="center" gap={16} style={{ minWidth: 0 }}>
          <Logo withText={!!wide} />
          {wide && me.data && (
            <Typography.Text strong ellipsis style={{ maxWidth: 220 }}>
              {me.data.tenantName}
            </Typography.Text>
          )}
          {/* Cashier bán ở chi nhánh trong token; chỉ người không gắn chi nhánh mới chọn. */}
          {branch.canChoose && (
            <Select
              value={branch.branchId}
              options={branch.options}
              placeholder="Chọn chi nhánh bán hàng"
              aria-label="Chi nhánh bán hàng"
              size="middle"
              style={{ width: wide ? 240 : 170 }}
              onChange={branch.choose}
            />
          )}
        </Flex>
        <Flex align="center" gap={12}>
          <Badge status={online ? 'success' : 'error'} text={wide ? (online ? 'Trực tuyến' : 'Mất mạng') : undefined} />
          {user && (
            <>
              <Divider orientation="vertical" style={{ height: 28, marginInline: 4 }} />
              <Avatar style={{ background: COLORS.primarySoft, color: COLORS.primary, fontWeight: 600 }}>{initialOf(user.fullName)}</Avatar>
              {wide && (
                <Flex vertical style={{ lineHeight: 1.3 }}>
                  <Typography.Text strong>{user.fullName}</Typography.Text>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {ROLE_LABELS[user.role]}
                  </Typography.Text>
                </Flex>
              )}
            </>
          )}
          <Button onClick={() => void logout()}>Đăng xuất</Button>
        </Flex>
      </Flex>
    </Layout.Header>
  );
}

// Không bán offline (ADR-0007): mất mạng thì báo và khóa nút thanh toán; giỏ vẫn giữ nguyên.
function OfflineBanner() {
  const online = useOnline();
  if (online) return null;
  return <Alert className="no-print" type="warning" banner title="Mất kết nối mạng. Chưa thanh toán được; giỏ hàng vẫn giữ nguyên." />;
}
