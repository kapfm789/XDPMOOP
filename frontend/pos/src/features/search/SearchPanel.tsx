import { formatMoney, type PosSku } from '@oism/shared';
import { Button, Card, Empty, Flex, Grid, Input, type InputRef, Tag, Typography, theme } from 'antd';
import type { Ref } from 'react';

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
  // Mở hộp quét bằng camera hoặc ảnh, cho quầy không có máy quét.
  onScan: () => void;
};

function SearchIcon() {
  return (
    <svg width={18} height={18} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" aria-hidden="true">
      <circle cx="11" cy="11" r="7" />
      <path d="m20 20-4-4" />
    </svg>
  );
}

function CameraIcon() {
  return (
    <svg width={22} height={22} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M4 8h3l2-3h6l2 3h3v11H4z" />
      <circle cx="12" cy="13" r="3.5" />
    </svg>
  );
}

// Ô tìm và tối đa 20 SKU kèm giá lẻ, tồn khả dụng, dạng lưới ô: docs/design/ui/pos.md mục "Bố cục và phong cách".
export function SearchPanel({ inputRef, autoFocus, text, onTextChange, skus, loading, inCart, onPick, onScan }: Props) {
  const { token } = theme.useToken();
  const screens = Grid.useBreakpoint();
  const columns = screens.xl ? 3 : screens.md ? 2 : 1;

  return (
    <Flex vertical gap={12}>
      <Card styles={{ body: { padding: 12, display: 'flex', gap: 8 } }}>
        <Input
          ref={inputRef}
          autoFocus={autoFocus}
          allowClear
          value={text}
          onChange={(event) => onTextChange(event.target.value)}
          placeholder="Tên, mã SKU hoặc quét mã vạch (F2)"
          aria-label="Tìm sản phẩm"
          prefix={
            <span style={{ color: token.colorTextSecondary, display: 'inline-flex', marginInlineEnd: 4 }}>
              <SearchIcon />
            </span>
          }
          suffix={loading ? <Typography.Text type="secondary">Đang tìm…</Typography.Text> : undefined}
          style={{ height: 52, fontSize: 16 }}
        />
        <Button
          icon={<CameraIcon />}
          onClick={onScan}
          aria-label="Quét mã vạch bằng camera hoặc ảnh"
          title="Quét bằng camera hoặc ảnh"
          style={{ height: 52, width: 52, flex: 'none' }}
        />
      </Card>
      {skus.length === 0 ? (
        <Card>
          <Empty description="Không có sản phẩm khớp" />
        </Card>
      ) : (
        <div role="list" aria-label="Kết quả tìm" style={{ display: 'grid', gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))`, gap: 12 }}>
          {skus.map((sku) => {
            const soldOut = sku.available <= 0;
            const full = !soldOut && inCart(sku.skuId) >= sku.available;
            return (
              <Card
                key={sku.skuId}
                role="listitem"
                hoverable={!soldOut}
                onClick={soldOut ? undefined : () => onPick(sku)}
                aria-disabled={soldOut}
                styles={{ body: { padding: 14, minHeight: 92, display: 'flex', flexDirection: 'column', justifyContent: 'space-between', gap: 8 } }}
                style={{ cursor: soldOut ? 'not-allowed' : 'pointer', opacity: soldOut ? 0.5 : 1 }}
              >
                <div style={{ minWidth: 0 }}>
                  <Typography.Text strong ellipsis={{ tooltip: sku.name }} style={{ display: 'block' }}>
                    {sku.name}
                  </Typography.Text>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {sku.skuCode}
                  </Typography.Text>
                </div>
                <Flex justify="space-between" align="center" gap={8}>
                  <Typography.Text strong style={{ fontSize: 16, color: token.colorPrimary }}>
                    {formatMoney(sku.retailPrice)}
                  </Typography.Text>
                  {soldOut ? (
                    <Tag color="red" style={{ marginInlineEnd: 0 }}>
                      Hết hàng
                    </Tag>
                  ) : (
                    <Tag color={full ? 'orange' : 'green'} style={{ marginInlineEnd: 0 }}>
                      Còn {sku.available}
                    </Tag>
                  )}
                </Flex>
              </Card>
            );
          })}
        </div>
      )}
    </Flex>
  );
}
