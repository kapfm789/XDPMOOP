import { Typography } from 'antd';
import { ReorderSuggestionsPanel } from '../../features/forecast/ReorderSuggestionsPanel';

export function ForecastPage() {
  return (
    <>
      <Typography.Title level={3}>Đề xuất nhập hàng</Typography.Title>
      <ReorderSuggestionsPanel />
    </>
  );
}
