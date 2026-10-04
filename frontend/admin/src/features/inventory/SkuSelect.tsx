import { getProduct, listProducts } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';
import { Select } from 'antd';
import { useEffect, useState } from 'react';

// Giá trị của ô chọn: id của SKU kèm nhãn để hiện lại khi sửa phiếu.
export type SkuOption = { value: string; label: string };

type Props = {
  // Form.Item của Ant Design truyền hai thuộc tính này.
  value?: SkuOption;
  onChange?: (sku: SkuOption) => void;
};

// Tìm SKU theo tên sản phẩm hoặc mã SKU để đưa vào chứng từ kho.
export function SkuSelect({ value, onChange }: Props) {
  const [typed, setTyped] = useState('');
  const [term, setTerm] = useState('');
  // Chờ người dùng ngừng gõ rồi mới tìm.
  useEffect(() => {
    const timer = setTimeout(() => setTerm(typed.trim()), 300);
    return () => clearTimeout(timer);
  }, [typed]);

  const found = useQuery({
    queryKey: ['sku-search', term],
    enabled: term.length > 0,
    queryFn: async (): Promise<SkuOption[]> => {
      // ponytail: catalog chỉ tìm theo sản phẩm, nên mỗi lần tìm gọi 1 + tối đa 10 request để lấy SKU của từng sản phẩm.
      // Khi có GET /api/core/pos/skus (W3-02) thì đổi sang một request.
      const products = await listProducts({ query: term, page: 1, pageSize: 10 });
      const details = await Promise.all(products.items.map((product) => getProduct(product.id)));
      return details
        .flatMap((product) => product.skus)
        .filter((sku) => sku.isActive)
        .map((sku) => ({ value: sku.id, label: `${sku.skuCode} · ${sku.name}` }));
    },
  });

  return (
    <Select<SkuOption, SkuOption>
      labelInValue
      showSearch={{ filterOption: false, onSearch: setTyped }}
      placeholder="Gõ tên sản phẩm hoặc mã SKU"
      aria-label="Chọn SKU"
      options={found.data}
      loading={found.isFetching}
      notFoundContent={term ? (found.isFetching ? 'Đang tìm...' : 'Không tìm thấy SKU') : 'Gõ để tìm'}
      value={value}
      onChange={(sku) => onChange?.({ value: sku.value, label: sku.label })}
      style={{ width: '100%' }}
    />
  );
}
