import { formatMoney, searchPosSkus } from '@oism/shared';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Select } from 'antd';
import { useEffect, useState } from 'react';

// Giá trị của ô chọn: id của SKU kèm nhãn, để lỗi thiếu hàng gọi được tên SKU.
export type PosSkuOption = { value: string; label: string };

type Props = {
  // Tồn khả dụng tính theo chi nhánh; chưa chọn chi nhánh thì chưa tìm được.
  branchId?: string;
  // Form.Item của Ant Design truyền hai thuộc tính này.
  value?: PosSkuOption;
  onChange?: (sku: PosSkuOption) => void;
};

// Tìm SKU đang bán theo tên, mã SKU hoặc mã vạch, kèm giá lẻ và tồn khả dụng tại chi nhánh (GET /api/core/pos/skus).
// Tồn ở đây chỉ để báo sớm; lúc tạo đơn backend mới là nơi quyết định.
export function PosSkuSelect({ branchId, value, onChange }: Props) {
  const [typed, setTyped] = useState('');
  const [term, setTerm] = useState('');
  // Chờ người dùng ngừng gõ rồi mới tìm.
  useEffect(() => {
    const timer = setTimeout(() => setTerm(typed.trim()), 300);
    return () => clearTimeout(timer);
  }, [typed]);

  const found = useQuery({
    queryKey: ['pos-skus', branchId, term],
    enabled: !!branchId,
    queryFn: () => searchPosSkus(branchId ?? '', term),
    placeholderData: keepPreviousData,
  });

  return (
    <Select<PosSkuOption, PosSkuOption & { hint: string }>
      labelInValue
      disabled={!branchId}
      showSearch={{ filterOption: false, onSearch: setTyped }}
      placeholder={branchId ? 'Gõ tên, mã SKU hoặc mã vạch' : 'Chọn chi nhánh trước'}
      aria-label="Chọn SKU"
      // Nhãn của giá trị đã chọn chỉ gồm mã và tên; giá lẻ và tồn khả dụng đổi theo thời gian nên chỉ hiện trong danh sách.
      options={(found.data ?? []).map((sku) => ({
        value: sku.skuId,
        label: `${sku.skuCode} · ${sku.name}`,
        hint: `${formatMoney(sku.retailPrice)} · còn ${sku.available}`,
      }))}
      optionRender={(option) => `${option.data.label} · ${option.data.hint}`}
      loading={found.isFetching}
      notFoundContent={found.isFetching ? 'Đang tìm...' : 'Không tìm thấy SKU'}
      value={value}
      onChange={(sku) => onChange?.({ value: sku.value, label: sku.label })}
      style={{ width: '100%' }}
    />
  );
}
