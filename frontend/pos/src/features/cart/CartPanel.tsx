import { formatMoney } from '@oism/shared';
import { Button, Empty, Flex, Typography } from 'antd';
import { LIST_STYLE, ROW_STYLE } from '../search/SearchPanel';
import { type CartLine, cartTotal } from './cart';

type Props = {
  cart: CartLine[];
  selected: string | undefined;
  onSelect: (skuId: string) => void;
  onQuantity: (skuId: string, quantity: number) => void;
  onRemove: (skuId: string) => void;
};

// Giỏ hàng: dòng hàng, số lượng, thành tiền, tổng. Dòng đang chọn nhận phím `+`, `-`, Delete.
export function CartPanel({ cart, selected, onSelect, onQuantity, onRemove }: Props) {
  return (
    <div style={LIST_STYLE} role="list" aria-label="Giỏ hàng">
      {cart.length === 0 && <Empty description="Giỏ hàng trống" style={{ padding: 24 }} />}
      {cart.map((line) => (
        <Flex
          key={line.skuId}
          role="listitem"
          vertical
          gap={4}
          onClick={() => onSelect(line.skuId)}
          style={{ ...ROW_STYLE, cursor: 'pointer', background: line.skuId === selected ? '#e6f4ff' : undefined }}
        >
          <Flex justify="space-between" gap={12}>
            <Typography.Text strong>{line.name}</Typography.Text>
            <Typography.Text strong>{formatMoney(line.quantity * line.unitPrice)}</Typography.Text>
          </Flex>
          <Flex justify="space-between" align="center" gap={12}>
            <Typography.Text type="secondary">
              {line.skuCode} · {formatMoney(line.unitPrice)}
              {line.quantity >= line.available && ` · chỉ còn ${line.available}`}
            </Typography.Text>
            {/* Bấm nút không được lấy mất dòng đang chọn của phím tắt, nên chặn click lan lên dòng. */}
            <Flex align="center" gap={8} onClick={(event) => event.stopPropagation()}>
              <Button aria-label={`Giảm ${line.name}`} onClick={() => onQuantity(line.skuId, line.quantity - 1)}>
                −
              </Button>
              <Typography.Text strong style={{ minWidth: 24, textAlign: 'center' }}>
                {line.quantity}
              </Typography.Text>
              <Button
                aria-label={`Tăng ${line.name}`}
                disabled={line.quantity >= line.available}
                onClick={() => onQuantity(line.skuId, line.quantity + 1)}
              >
                +
              </Button>
              <Button danger aria-label={`Xóa ${line.name}`} onClick={() => onRemove(line.skuId)}>
                ×
              </Button>
            </Flex>
          </Flex>
        </Flex>
      ))}
      <Flex justify="space-between" align="center" style={{ padding: '12px 16px' }}>
        <Typography.Text strong>Tổng</Typography.Text>
        <Typography.Title level={3} style={{ margin: 0 }}>
          {formatMoney(cartTotal(cart))}
        </Typography.Title>
      </Flex>
    </div>
  );
}
