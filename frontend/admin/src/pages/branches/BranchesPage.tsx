import { Typography } from 'antd';
import { BranchesPanel } from '../../features/branches/BranchesPanel';

export function BranchesPage() {
  return (
    <>
      <Typography.Title level={3}>Chi nhánh</Typography.Title>
      <BranchesPanel />
    </>
  );
}
