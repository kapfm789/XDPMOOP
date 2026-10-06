import { formatMoney, type PosSku } from '@oism/shared';
import { Empty, Flex, Input, type InputRef, Tag, Typography } from 'antd';
import type { CSSProperties, Ref } from 'react';

// Khung và dòng dùng chung cho danh sách kết quả và giỏ hàng; mỗi dòng cao tối thiểu 44 px để chạm bằng ngón tay.
export const LIST_STYLE: CSSProperties = { border: '1px solid #d9d9d9', borderRadius: 8, background: '#fff', overflow: 'hidden' };
export const ROW_STYLE: CSSProperties = { minHeight: 44, padding: '10px 16px', borderBottom: '1px solid #f0f0f0', boxSizing: 'border-box' };

type Props = {
  inputRef: Ref<InputRef>;
  // Có bàn phím hoặc máy quét thì ô tìm nhận focus ngay khi mở màn hình.
  autoFocus: boolean;
  text: string;
  onTextChange: (text: string) => void;
  skus: PosSku[];
  loading: boolean;
  // Số lượng đang có trong giỏ của từng SKU, để báo sớm khi đã chạm tồn khả dụng.
  inCart: (skuId: string) => number;
  onPick: (sku: PosSku) => void;
};

// Ô tìm và tối đa 20 SKU kèm giá lẻ, tồn khả dụng: docs/design/ui/pos.md mục "Bố cục màn hình bán hàng".
export function SearchPanel({ inputRef, autoFocus, text, onTextChange, skus, loading, inCart, onPick }: Props) {
  return (
    <Flex vertical gap={12}>
      <Input.Search
        ref={inputRef}
        autoFocus={autoFocus}
        allowClear
        loading={loading}
        value={text}
        onChange={(event) => onTextChange(event.target.value)}
        placeholder="Tên, mã SKU hoặc quét mã vạch (F2)"
        aria-label="Tìm sản phẩm"
      />
      <div style={LIST_STYLE} role="list" aria-label="Kết quả tìm">
        {skus.length === 0 && <Empty description="Không có sản phẩm khớp" style={{ padding: 24 }} />}
        {skus.map((sku) => {
          const soldOut = sku.available <= 0;
          const full = !soldOut && inCart(sku.skuId) >= sku.available;
          return (
            <Flex
              key={sku.skuId}
              role="listitem"
              justify="space-between"
              align="center"
              gap={12}
              onClick={soldOut ? undefined : () => onPick(sku)}
              style={{ ...ROW_STYLE, cursor: soldOut ? 'not-allowed' : 'pointer', opacity: soldOut ? 0.5 : 1 }}
            >
              <div>
                <Typography.Text strong>{sku.name}</Typography.Text>
                <br />
                <Typography.Text type="secondary">{sku.skuCode}</Typography.Text>
              </div>
              <Flex vertical align="end">
                <Typography.Text strong>{formatMoney(sku.retailPrice)}</Typography.Text>
                {soldOut ? <Tag color="red">Hết hàng</Tag> : <Tag color={full ? 'orange' : 'green'}>Còn {sku.available}</Tag>}
              </Flex>
            </Flex>
          );
        })}
      </div>
    </Flex>
  );
}
