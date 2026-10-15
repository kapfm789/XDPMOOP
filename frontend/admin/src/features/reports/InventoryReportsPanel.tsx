import { errorMessage } from '@oism/shared';
import { Alert, Flex, Select, Tabs } from 'antd';
import { useState } from 'react';
import { useBranchOptions } from '../inventory/useBranchOptions';
import { InventoryValueTable } from './InventoryValueTable';
import { SellersTable } from './SellersTable';

const DAY_OPTIONS = [7, 30, 90].map((days) => ({ value: days, label: `${days} ngày gần nhất` }));

// UC-REP-02: giá trị tồn, bán chạy, bán chậm, cùng bộ lọc chi nhánh và khoảng ngày bán.
export function InventoryReportsPanel() {
  const [branchId, setBranchId] = useState<string>();
  const [days, setDays] = useState(30);
  const branches = useBranchOptions();

  return (
    <>
      <Flex gap={8} wrap style={{ marginBottom: 16 }}>
        <Select
          allowClear
          placeholder="Mọi chi nhánh"
          aria-label="Lọc theo chi nhánh"
          options={branches.options}
          value={branchId}
          onChange={setBranchId}
          style={{ width: 240 }}
        />
        <Select aria-label="Khoảng thời gian bán" options={DAY_OPTIONS} value={days} onChange={setDays} style={{ width: 180 }} />
      </Flex>
      {branches.error && <Alert type="error" showIcon title={errorMessage(branches.error)} style={{ marginBottom: 16 }} />}
      <Tabs
        items={[
          {
            key: 'value',
            label: 'Giá trị tồn',
            // Đổi chi nhánh thì dựng lại bảng để quay về trang đầu.
            children: <InventoryValueTable key={branchId ?? 'all'} branchId={branchId} branchName={branches.name} />,
          },
          { key: 'top', label: 'Bán chạy', children: <SellersTable kind="top" branchId={branchId} days={days} /> },
          { key: 'slow', label: 'Bán chậm', children: <SellersTable kind="slow" branchId={branchId} days={days} /> },
        ]}
      />
    </>
  );
}
