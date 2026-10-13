import { Typography } from 'antd';
import { ChannelShopsPanel } from '../../features/channel/ChannelShopsPanel';
import { WebhookEventsPanel } from '../../features/channel/WebhookEventsPanel';

export function ChannelPage() {
  return (
    <>
      <Typography.Title level={3}>Kênh sàn</Typography.Title>
      <Typography.Title level={4}>Shop của sàn</Typography.Title>
      <ChannelShopsPanel />
      <Typography.Title level={4} style={{ marginTop: 32 }}>
        Webhook đã nhận
      </Typography.Title>
      <WebhookEventsPanel />
    </>
  );
}
