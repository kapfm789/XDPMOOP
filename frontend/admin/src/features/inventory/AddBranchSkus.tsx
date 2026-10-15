import { errorMessage, listStock } from '@oism/shared';
import { useMutation } from '@tanstack/react-query';
import { Button, Form, message } from 'antd';
import type { SkuOption } from './SkuSelect';

// Một dòng số đếm của form kiểm kê.
export type CountLine = { sku: SkuOption; countedQty: number };

type Props = { branchId: string; add: (line: Partial<CountLine>) => void };

// Thêm một dòng trống cho mỗi SKU đang có số dư ở chi nhánh mà danh sách chưa có, để đếm hết kho không phải chọn từng SKU.
export function AddBranchSkus({ branchId, add }: Props) {
  const form = Form.useFormInstance<{ items?: (CountLine | undefined)[] }>();
  const load = useMutation({
    mutationFn: async () => {
      const lines: (CountLine | undefined)[] = form.getFieldValue('items') ?? [];
      const present = new Set(lines.flatMap((line) => (line?.sku ? [line.sku.value] : [])));
      let added = 0;
      for (let page = 1; ; page++) {
        const stock = await listStock({ branchId, page, pageSize: 100 });
        for (const row of stock.items.filter((row) => !present.has(row.skuId))) {
          add({ sku: { value: row.skuId, label: `${row.skuCode} · ${row.name}` } });
          added++;
        }
        if (page * stock.pageSize >= stock.total) return added;
      }
    },
    onSuccess: (added) => void message.info(added ? `Đã thêm ${added} SKU` : 'Mọi SKU của chi nhánh đã có trong danh sách'),
    onError: (failure) => void message.error(errorMessage(failure)),
  });

  return (
    <Button loading={load.isPending} onClick={() => load.mutate()}>
      Thêm mọi SKU có ở chi nhánh
    </Button>
  );
}
