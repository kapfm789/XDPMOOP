import type { SkuRequest } from '@oism/shared';
import { Button, Flex, Form, Input, InputNumber } from 'antd';

export type AttributePair = { name: string; value: string };

export type SkuValues = { skuCode: string; attributes?: AttributePair[]; retailPrice?: number; wholesalePrice?: number };

// Form giữ thuộc tính dạng danh sách cặp tên và giá trị; API nhận một object.
export const toAttributes = (pairs?: AttributePair[]): Record<string, string> =>
  Object.fromEntries((pairs ?? []).map(({ name, value }) => [name.trim(), value.trim()]));

export const toPairs = (attributes: Record<string, string>): AttributePair[] =>
  Object.entries(attributes).map(([name, value]) => ({ name, value }));

export const toSkuRequest = ({ skuCode, attributes, retailPrice, wholesalePrice }: SkuValues): SkuRequest => ({
  skuCode,
  attributes: toAttributes(attributes),
  retailPrice,
  wholesalePrice,
});

type Props = {
  // Đường dẫn tới SKU trong form: rỗng khi form chỉ có một SKU, [field.name] khi SKU nằm trong một Form.List.
  path?: (string | number)[];
  code?: boolean;
  attributes?: boolean;
  // Bỏ trống thì không hiện ô giá. Chỉ Owner đặt được giá; backend bỏ qua giá do Staff gửi.
  prices?: 'optional' | 'required';
};

// Các ô của một SKU: mã, thuộc tính (ví dụ Màu: Đen, Size: M), giá lẻ và giá sỉ.
export function SkuFields({ path = [], code, attributes, prices }: Props) {
  const priceRules = [{ required: prices === 'required', message: 'Nhập giá' }];

  return (
    <>
      {code && (
        <Form.Item name={[...path, 'skuCode']} label="Mã SKU" rules={[{ required: true, whitespace: true, message: 'Nhập mã SKU' }]}>
          <Input maxLength={64} />
        </Form.Item>
      )}
      {attributes && (
        <Form.List name={[...path, 'attributes']}>
          {(fields, { add, remove }) => (
            <Form.Item label="Thuộc tính">
              {fields.map((field) => (
                <Flex key={field.key} gap={8} align="baseline">
                  <Form.Item
                    name={[field.name, 'name']}
                    rules={[{ required: true, whitespace: true, message: 'Nhập tên thuộc tính' }]}
                    style={{ flex: 1, marginBottom: 8 }}
                  >
                    <Input placeholder="Tên, ví dụ Màu" maxLength={100} aria-label="Tên thuộc tính" />
                  </Form.Item>
                  <Form.Item
                    name={[field.name, 'value']}
                    rules={[{ required: true, whitespace: true, message: 'Nhập giá trị' }]}
                    style={{ flex: 1, marginBottom: 8 }}
                  >
                    <Input placeholder="Giá trị, ví dụ Đen" maxLength={100} aria-label="Giá trị thuộc tính" />
                  </Form.Item>
                  <Button type="link" danger onClick={() => remove(field.name)}>
                    Xóa
                  </Button>
                </Flex>
              ))}
              <Button type="dashed" onClick={() => add()}>
                Thêm thuộc tính
              </Button>
            </Form.Item>
          )}
        </Form.List>
      )}
      {prices && (
        <Flex gap={16}>
          <Form.Item name={[...path, 'retailPrice']} label="Giá lẻ" rules={priceRules} style={{ flex: 1 }}>
            <InputNumber min={0} step={1000} suffix="₫" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name={[...path, 'wholesalePrice']} label="Giá sỉ" rules={priceRules} style={{ flex: 1 }}>
            <InputNumber min={0} step={1000} suffix="₫" style={{ width: '100%' }} />
          </Form.Item>
        </Flex>
      )}
    </>
  );
}
