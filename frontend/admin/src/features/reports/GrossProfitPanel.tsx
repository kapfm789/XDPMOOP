import {
  errorMessage,
  formatDate,
  formatMoney,
  formatNumber,
  getGrossProfit,
  type GrossProfitGroupBy,
  type GrossProfitRow,
  type OrderChannel,
  vietnamDate,
} from '@oism/shared';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Alert, Card, Col, DatePicker, Flex, Row, Select, Statistic, Table } from 'antd';
import { useState } from 'react';
import { SkuSelect, type SkuOption } from '../inventory/SkuSelect';
import { useBranchOptions } from '../inventory/useBranchOptions';
import { CHANNEL_LABELS } from '../orders/labels';

// from và to bỏ trống là 30 ngày gần nhất tính cả hôm nay.
type Filter = { from?: string; to?: string; branchId?: string; channel?: OrderChannel; sku?: SkuOption; groupBy: GrossProfitGroupBy };

const DEFAULT_DAYS = 30;

const GROUP_OPTIONS: { value: GrossProfitGroupBy; label: string }[] = [
  { value: 'day', label: 'Theo ngày' },
  { value: 'branch', label: 'Theo chi nhánh' },
  { value: 'channel', label: 'Theo kênh' },
  { value: 'sku', label: 'Theo SKU' },
];

const CHANNEL_OPTIONS = (Object.keys(CHANNEL_LABELS) as OrderChannel[]).map((value) => ({ value, label: CHANNEL_LABELS[value] }));

// UC-REP-01: doanh thu thuần, giá vốn hàng bán và lợi nhuận gộp, lọc theo thời gian, chi nhánh, kênh, SKU.
// Mọi con số do backend tính theo ADR-0008; giao diện chỉ hiển thị.
export function GrossProfitPanel() {
  const [filter, setFilter] = useState<Filter>({ groupBy: 'day' });
  const branches = useBranchOptions();
  const now = new Date();
  const from = filter.from ?? vietnamDate(now, 1 - DEFAULT_DAYS);
  const to = filter.to ?? vietnamDate(now);
  const query = { from, to, branchId: filter.branchId, channel: filter.channel, skuId: filter.sku?.value, groupBy: filter.groupBy };
  const report = useQuery({
    queryKey: ['gross-profit', query],
    queryFn: () => getGrossProfit(query),
    placeholderData: keepPreviousData,
  });
  const change = (part: Partial<Filter>) => setFilter((current) => ({ ...current, ...part }));
  const totals = report.data?.totals;
  const error = report.error ?? branches.error;

  const label = (row: GrossProfitRow) => {
    switch (filter.groupBy) {
      case 'day':
        return formatDate(row.key);
      case 'branch':
        return branches.name(row.key) || row.key;
      case 'channel':
        return CHANNEL_LABELS[row.key as OrderChannel] ?? row.key;
      case 'sku':
        return row.skuCode ? `${row.skuCode} · ${row.name}` : row.key;
    }
  };

  return (
    <>
      <Flex gap={8} wrap style={{ marginBottom: 16 }}>
        <DatePicker.RangePicker
          aria-label="Khoảng ngày"
          format="DD/MM/YYYY"
          // Để trống là 30 ngày gần nhất; placeholder cho biết khoảng đang xem.
          placeholder={[formatDate(from), formatDate(to)]}
          onChange={(range) => change({ from: range?.[0]?.format('YYYY-MM-DD'), to: range?.[1]?.format('YYYY-MM-DD') })}
        />
        <Select
          allowClear
          placeholder="Mọi chi nhánh"
          aria-label="Lọc theo chi nhánh"
          options={branches.options}
          value={filter.branchId}
          onChange={(branchId?: string) => change({ branchId })}
          style={{ width: 220 }}
        />
        <Select
          allowClear
          placeholder="Mọi kênh"
          aria-label="Lọc theo kênh"
          options={CHANNEL_OPTIONS}
          value={filter.channel}
          onChange={(channel?: OrderChannel) => change({ channel })}
          style={{ width: 140 }}
        />
        <div style={{ width: 280 }}>
          <SkuSelect allowClear value={filter.sku} onChange={(sku) => change({ sku })} />
        </div>
        <Select
          aria-label="Nhóm theo"
          options={GROUP_OPTIONS}
          value={filter.groupBy}
          onChange={(groupBy: GrossProfitGroupBy) => change({ groupBy })}
          style={{ width: 160 }}
        />
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title="Doanh thu thuần" value={totals ? formatMoney(totals.revenue) : '–'} loading={report.isPending} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title="Giá vốn hàng bán" value={totals ? formatMoney(totals.cogs) : '–'} loading={report.isPending} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title="Lợi nhuận gộp" value={totals ? formatMoney(totals.grossProfit) : '–'} loading={report.isPending} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title="Số lượng bán" value={totals ? formatNumber(totals.quantity) : '–'} loading={report.isPending} />
          </Card>
        </Col>
      </Row>
      <Table<GrossProfitRow>
        scroll={{ x: 'max-content' }}
        rowKey="key"
        loading={report.isFetching}
        dataSource={report.data?.rows}
        pagination={{ pageSize: 31, showSizeChanger: false, hideOnSinglePage: true }}
        locale={{ emptyText: 'Không có đơn đã duyệt nào khớp bộ lọc' }}
        columns={[
          { title: GROUP_OPTIONS.find((option) => option.value === filter.groupBy)?.label.replace('Theo ', ''), render: (_, row) => label(row) },
          { title: 'Số lượng', dataIndex: 'quantity', align: 'right', render: (quantity: number) => formatNumber(quantity) },
          { title: 'Doanh thu thuần', dataIndex: 'revenue', align: 'right', render: (amount: number) => formatMoney(amount) },
          { title: 'Giá vốn', dataIndex: 'cogs', align: 'right', render: (amount: number) => formatMoney(amount) },
          { title: 'Lợi nhuận gộp', dataIndex: 'grossProfit', align: 'right', render: (amount: number) => formatMoney(amount) },
        ]}
      />
    </>
  );
}
