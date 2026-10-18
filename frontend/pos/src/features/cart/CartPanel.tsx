import { formatMoney } from '@oism/shared';
import { Badge, Button, Card, Empty, Flex, Typography, theme } from 'antd';
import { type CartLine, cartCount, cartTotal } from './cart';

type Props = {
  cart: CartLine[];
  selected: string | undefined;
  onSelect: (skuId: string) => void;
  onQuantity: (skuId: string, quantity: number) => void;
  onRemove: (skuId: string) => void;
};

// Thẻ giỏ hàng: dòng hàng, số lượng, thành tiền, tổng (docs/design/ui/pos.md). Dòng đang chọn nhận phím `+`, `-`, Delete.
export function CartPanel({ cart, selected, onSelect, onQuantity, onRemove }: Props) {
  const { token } = theme.useToken();

  return (
    <Card
      title={
        <Flex align="center" gap={8}>
          Giỏ hàng
          <Badge count={cartCount(cart)} showZero color={cart.length > 0 ? token.colorPrimary : token.colorTextQuaternary} />
        </Flex>
      }
      styles={{ body: { padding: 0 } }}
    >
      <div role="list" aria-label="Giỏ hàng">
        {cart.length === 0 && <Empty description="Giỏ hàng trống. Quét mã hoặc chọn sản phẩm bên trái." style={{ padding: 24 }} />}
        {cart.map((line) => {
          const active = line.skuId === selected;
          return (
            <Flex
              key={line.skuId}
              role="listitem"
              vertical
              gap={6}
              onClick={() => onSelect(line.skuId)}
              style={{
                minHeight: 44,
                padding: '12px 16px',
                borderBottom: `1px solid ${token.colorBorderSecondary}`,
                borderInlineStart: `3px solid ${active ? token.colorPrimary : 'transparent'}`,
                background: active ? token.colorPrimaryBg : undefined,
                cursor: 'pointer',
              }}
            >
              <Flex justify="space-between" gap={12}>
                <Typography.Text strong>{line.name}</Typography.Text>
                <Typography.Text strong>{formatMoney(line.quantity * line.unitPrice)}</Typography.Text>
              </Flex>
              <Flex justify="space-between" align="center" gap={12}>
                <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                  {line.skuCode} · {formatMoney(line.unitPrice)}
                  {line.quantity >= line.available && <Typography.Text type="warning"> · chỉ còn {line.available}</Typography.Text>}
                </Typography.Text>
                {/* Bấm nút không được lấy mất dòng đang chọn của phím tắt, nên chặn click lan lên dòng. */}
                <Flex align="center" gap={6} onClick={(event) => event.stopPropagation()}>
                  <Button aria-label={`Giảm ${line.name}`} onClick={() => onQuantity(line.skuId, line.quantity - 1)}>
                    −
                  </Button>
                  <Typography.Text strong style={{ minWidth: 28, textAlign: 'center', fontSize: 16 }}>
                    {line.quantity}
                  </Typography.Text>
                  <Button
                    aria-label={`Tăng ${line.name}`}
                    disabled={line.quantity >= line.available}
                    onClick={() => onQuantity(line.skuId, line.quantity + 1)}
                  >
                    +
                  </Button>
                  <Button danger type="text" aria-label={`Xóa ${line.name}`} onClick={() => onRemove(line.skuId)}>
                    ×
                  </Button>
                </Flex>
              </Flex>
            </Flex>
          );
        })}
      </div>
      <Flex justify="space-between" align="center" style={{ padding: '14px 16px', background: token.colorFillAlter }}>
        <Typography.Text type="secondary">Tổng</Typography.Text>
        <Typography.Title level={3} style={{ margin: 0 }}>
          {formatMoney(cartTotal(cart))}
        </Typography.Title>
      </Flex>
    </Card>
  );
}
