import {
  errorMessage,
  formatDateTime,
  formatMoney,
  type LedgerLine,
  type LedgerReason,
  type LedgerReference,
  type LedgerType,
  listLedger,
  useAuth,
} from '@oism/shared';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Alert, DatePicker, Flex, Select, Table, type TableColumnsType, Tag } from 'antd';
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useBranchOptions } from './useBranchOptions';

type Filter = { branchId?: string; from?: string; to?: string; page: number };

const PAGE_SIZE = 20;

const REASON_LABELS: Record<LedgerReason, string> = {
  Purchase: 'Nhập mua',
  Sale: 'Bán hàng',
  TransferOut: 'Xuất chuyển kho',
  TransferIn: 'Nhận chuyển kho',
  StocktakeAdjust: 'Điều chỉnh kiểm kê',
  ReturnIn: 'Khách trả hàng',
  Reversal: 'Bút toán đảo',
};

const REFERENCE_LABELS: Record<LedgerReference, string> = {
  PurchaseReceipt: 'Phiếu nhập',
  Order: 'Đơn hàng',
  StockTransfer: 'Phiếu chuyển kho',
  Stocktake: 'Phiên kiểm kê',
};

// UC-INV-01 AC-2: sổ giao dịch theo thứ tự ghi. Màn hình chỉ xem: không có nút sửa hay xóa (NFR-SEC-03).
// Đơn giá chỉ có trong phản hồi cho Owner.
export function LedgerPanel() {
  const isOwner = useAuth().user?.role === 'Owner';
  // Màn hình tồn mở sổ của một SKU tại một chi nhánh qua tham số trên URL.
  const [params, setParams] = useSearchParams();
  const skuId = params.get('skuId') ?? undefined;
  const [filter, setFilter] = useState<Filter>({ branchId: params.get('branchId') ?? undefined, page: 1 });
  const branches = useBranchOptions();
  const ledger = useQuery({
    queryKey: ['ledger', filter, skuId],
    queryFn: () => listLedger({ ...filter, skuId, pageSize: PAGE_SIZE }),
    placeholderData: keepPreviousData,
  });
  const error = ledger.error ?? branches.error;
  const narrow = (change: Partial<Filter>) => setFilter((current) => ({ ...current, ...change, page: 1 }));

  const columns: TableColumnsType<LedgerLine> = [
    { title: 'Số thứ tự', dataIndex: 'seq', align: 'right' },
    { title: 'Thời gian', dataIndex: 'createdAt', render: (createdAt: string) => formatDateTime(createdAt) },
    { title: 'Chi nhánh', dataIndex: 'branchId', render: (id: string) => branches.name(id) },
    { title: 'Mã SKU', dataIndex: 'skuCode' },
    { title: 'Tên', dataIndex: 'name' },
    {
      title: 'Loại',
      dataIndex: 'type',
      render: (type: LedgerType) => (type === 'IN' ? <Tag color="green">Nhập</Tag> : <Tag color="volcano">Xuất</Tag>),
    },
    { title: 'Lý do', dataIndex: 'reason', render: (reason: LedgerReason) => REASON_LABELS[reason] },
    { title: 'Số lượng', dataIndex: 'quantity', align: 'right' },
    { title: 'Tồn sau giao dịch', dataIndex: 'balanceAfter', align: 'right' },
    ...(isOwner
      ? ([
          {
            title: 'Đơn giá',
            dataIndex: 'unitCost',
            align: 'right',
            render: (unitCost?: number) => (unitCost === undefined ? '' : formatMoney(unitCost)),
          },
        ] satisfies TableColumnsType<LedgerLine>)
      : []),
    { title: 'Chứng từ', dataIndex: 'referenceType', render: (type: LedgerReference) => REFERENCE_LABELS[type] },
  ];

  return (
    <>
      <Flex gap={8} wrap align="center" style={{ marginBottom: 16 }}>
        <Select
          allowClear
          placeholder="Mọi chi nhánh"
          aria-label="Lọc theo chi nhánh"
          options={branches.options}
          value={filter.branchId}
          onChange={(branchId?: string) => narrow({ branchId })}
          style={{ width: 240 }}
        />
        <DatePicker.RangePicker
          aria-label="Lọc theo ngày ghi"
          format="DD/MM/YYYY"
          // Tính cả hai đầu: từ đầu ngày bắt đầu tới hết ngày kết thúc, theo giờ của máy đang dùng.
          onChange={(range) =>
            narrow({ from: range?.[0]?.startOf('day').toISOString(), to: range?.[1]?.endOf('day').toISOString() })
          }
        />
        {skuId && (
          <Tag closable onClose={() => setParams({})}>
            Đang xem một SKU
          </Tag>
        )}
      </Flex>
      {error && <Alert type="error" showIcon title={errorMessage(error)} style={{ marginBottom: 16 }} />}
      <Table<LedgerLine>
        scroll={{ x: 'max-content' }}
        rowKey="seq"
        loading={ledger.isPending}
        dataSource={ledger.data?.items}
        columns={columns}
        pagination={{
          current: filter.page,
          pageSize: PAGE_SIZE,
          total: ledger.data?.total,
          showSizeChanger: false,
          onChange: (page) => setFilter((current) => ({ ...current, page })),
        }}
      />
    </>
  );
}
