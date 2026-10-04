import { useAuth } from '@oism/shared';
import { Typography } from 'antd';

// Cảnh báo tồn và đơn chờ duyệt được dựng ở task W4-07.
export function HomePage() {
  const { user } = useAuth();

  return (
    <>
      <Typography.Title level={3}>Tổng quan</Typography.Title>
      <Typography.Paragraph>Xin chào, {user?.fullName}.</Typography.Paragraph>
    </>
  );
}
