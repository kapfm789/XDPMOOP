import {
  errorMessage,
  formatDateTime,
  formatNumber,
  formatPercent,
  getReorderSuggestions,
  type ReorderSuggestion,
  runForecast,
} from '@oism/shared';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Descriptions, Flex, message, Select, Table, Tag, Typography } from 'antd';
import { useState } from 'react';
import { useBranchOptions } from '../inventory/useBranchOptions';

const PAGE_SIZE = 20;

// UC-AI-01: đề xuất nhập hàng của lần chạy dự báo thành công gần nhất. UC-AI-02: chạy tay và xem WAPE của backtest.
// Đề xuất chỉ là khuyến nghị: màn hình không có nút nào biến nó thành phiếu nhập (D-14).
export function ReorderSuggestionsPanel() {
  const queryClient = useQueryClient();
  const [filter, setFilter] = useState<{ branchId?: string; page: number }>({ page: 1 });
  const branches = useBranchOptions();
  const suggestions = useQuery({
    queryKey: ['reorder-suggestions', filter],
    queryFn: () => getReorderSuggestions({ ...filter, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });
  const run = useMutation({
    mutationFn: runForecast,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['reorder-suggestions'] });
      void message.success('Đã chạy dự báo');
    },
    onError: (failure) => void message.error(errorMessage(failure)),
  });
  const latest = suggestions.data?.run;
  const succeeded = suggestions.data?.lastSucceededRun;
  const error = suggestions.error ?? branches.error;

  return (
    <>
      <Card style={{ marginBottom: 16 }}>
        <Flex justify="space-between" align="start" gap={16} wrap>
          <Descriptions
            size="small"
            column={1}
            items={[
              {
                label: 'Lần chạy gần nhất',
                children: latest ? (
                  <>
                    {formatDateTime(latest.ranAt)}{' '}
                    {latest.status === 'Succeeded' ? <Tag color="green">Thành công</Tag> : <Tag color="red">Lỗi</Tag>}
                  </>
                ) : (
                  'Chưa chạy lần nào'
                ),
              },
              {
                label: 'Sai số backtest (WAPE)',
                children: !succeeded
                  ? '–'
                  : succeeded.forecastWape === null || succeeded.baselineWape === null
                    ? 'Không có dữ liệu để đánh giá'
                    : `Dự báo ${formatPercent(succeeded.forecastWape)} · trung bình 7 ngày ${formatPercent(succeeded.baselineWape)}`,
              },
            ]}
          />
          <Button type="primary" loading={run.isPending} onClick={() => run.mutate()}>
            Chạy dự báo
          </Button>
        </Flex>
        {latest?.status === 'Failed' && (
          <Alert
            type="error"
            showIcon
            style={{ marginTop: 12 }}
            title={`Lần chạy lúc ${formatDateTime(latest.ranAt)} bị lỗi: ${latest.error ?? ''}`}
            description={
              succeeded ? `Đang hiện kết quả của lần chạy thành công lúc ${formatDateTime(succeeded.ranAt)}.` : 'Chưa có lần chạy nào thành công.'
            }
          />
        )}
        <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
          Nhu cầu 14 ngày dự báo bằng san bằng hàm mũ trên 60 ngày bán gần nhất. Số đề xuất đủ dùng cho 10 ngày (7 ngày chờ hàng và
          3 ngày dự phòng) trừ tồn khả dụng. Đây chỉ là khuyến nghị; hệ thống không tự tạo phiếu nhập.
        </Typography.Paragraph>
      </Card>
      <Flex gap={8} wrap style={{ marginBottom: 16 }}>
        <Select
          allowClear
          placeholder="Mọi chi nhánh"
          aria-label="Lọc theo chi nhánh"
          options={branches.options}
          value={filter.branchId}
          onChange={(branchId?: string) => setFilter({ branchId, page: 1 })}
          style={{ width: 240 }}
        />
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<ReorderSuggestion>
        scroll={{ x: 'max-content' }}
        rowKey={(row) => `${row.branchId}-${row.skuId}`}
        loading={suggestions.isFetching}
        dataSource={suggestions.data?.items}
        locale={{ emptyText: 'Chưa có kết quả dự báo; bấm "Chạy dự báo"' }}
        pagination={{
          current: filter.page,
          pageSize: PAGE_SIZE,
          total: suggestions.data?.total,
          showSizeChanger: false,
          onChange: (page) => setFilter((current) => ({ ...current, page })),
        }}
        columns={[
          { title: 'SKU', render: (_, row) => (row.skuCode ? `${row.skuCode} · ${row.name}` : row.skuId) },
          { title: 'Chi nhánh', dataIndex: 'branchId', render: (id: string) => branches.name(id) },
          {
            title: 'Nhu cầu 14 ngày',
            align: 'right',
            render: (_, row) =>
              row.hasEnoughData && row.forecastQty !== null ? (
                formatNumber(row.forecastQty, 1)
              ) : (
                <Typography.Text type="secondary">Chưa đủ dữ liệu</Typography.Text>
              ),
          },
          { title: 'Tồn khả dụng', dataIndex: 'available', align: 'right', render: (available: number) => formatNumber(available) },
          {
            title: 'Đề xuất nhập',
            align: 'right',
            render: (_, row) =>
              row.suggestedReorderQty === null ? '' : <Typography.Text strong={row.suggestedReorderQty > 0}>{formatNumber(row.suggestedReorderQty)}</Typography.Text>,
          },
        ]}
      />
    </>
  );
}
