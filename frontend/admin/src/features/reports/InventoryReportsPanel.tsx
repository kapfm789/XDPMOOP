import { errorMessage } from '@oism/shared';
import { Alert, Card, Select } from 'antd';
import { useState } from 'react';
import { FilterBar } from '../common/FilterBar';
import { useBranchOptions } from '../inventory/useBranchOptions';
import { InventoryValueTable } from './InventoryValueTable';
import { SellersTable } from './SellersTable';

const DAY_OPTIONS = [7, 30, 90].map((days) => ({ value: days, label: `${days} ngày gần nhất` }));

type Tab = 'value' | 'top' | 'slow';

const TABS: { key: Tab; label: string }[] = [
  { key: 'value', label: 'Giá trị tồn' },
  { key: 'top', label: 'Bán chạy' },
  { key: 'slow', label: 'Bán chậm' },
];

// UC-REP-02: giá trị tồn, bán chạy, bán chậm, cùng bộ lọc chi nhánh và khoảng ngày bán.
export function InventoryReportsPanel() {
  const [branchId, setBranchId] = useState<string>();
  const [days, setDays] = useState(30);
  const [tab, setTab] = useState<Tab>('value');
  const branches = useBranchOptions();

  return (
    <>
      <FilterBar>
        <Select
          allowClear
          placeholder="Mọi chi nhánh"
          aria-label="Lọc theo chi nhánh"
          options={branches.options}
          value={branchId}
          onChange={setBranchId}
          style={{ width: 240 }}
        />
        {/* Khoảng ngày bán chỉ áp cho bán chạy và bán chậm; giá trị tồn là số hiện tại. */}
        <Select
          aria-label="Khoảng thời gian bán"
          options={DAY_OPTIONS}
          value={days}
          onChange={setDays}
          disabled={tab === 'value'}
          style={{ width: 180 }}
        />
      </FilterBar>
      {branches.error && <Alert type="error" showIcon title={errorMessage(branches.error)} style={{ marginBottom: 16 }} />}
      <Card activeTabKey={tab} onTabChange={(key) => setTab(key as Tab)} tabList={TABS}>
        {/* Đổi chi nhánh thì dựng lại bảng để quay về trang đầu. */}
        {tab === 'value' && <InventoryValueTable key={branchId ?? 'all'} branchId={branchId} branchName={branches.name} />}
        {tab !== 'value' && <SellersTable kind={tab} branchId={branchId} days={days} />}
      </Card>
    </>
  );
}
