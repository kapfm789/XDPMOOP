import { Flex } from 'antd';
import { PageHeader } from '../../app/PageHeader';
import { ChannelShopsPanel } from '../../features/channel/ChannelShopsPanel';
import { WebhookEventsPanel } from '../../features/channel/WebhookEventsPanel';

export function ChannelPage() {
  return (
    <>
      <PageHeader />
      <Flex vertical gap={16}>
        <ChannelShopsPanel />
        <WebhookEventsPanel />
      </Flex>
    </>
  );
}
