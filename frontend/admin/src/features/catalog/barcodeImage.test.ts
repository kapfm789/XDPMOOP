import { expect, test } from 'vitest';
import { barcodeModules, barcodeSvg } from './barcodeImage';

test('EAN-13 has 95 modules with start, middle and end guards', () => {
  const modules = barcodeModules('2000000000015', 'EAN13');
  expect(modules).toHaveLength(95);
  expect(modules.slice(0, 3)).toBe('101');
  expect(modules.slice(45, 50)).toBe('01010');
  expect(modules.slice(92)).toBe('101');
});

test('EAN-13 left half follows the parity of the first digit', () => {
  // Chữ số đầu 4 chọn LGLLGG: chữ số 0 ở vị trí thứ hai dùng mẫu G.
  expect(barcodeModules('4006381333931', 'EAN13').slice(3, 17)).toBe('0001101' + '0100111');
});

test('EAN-13 rejects anything but 13 digits', () => {
  expect(() => barcodeModules('200000000001', 'EAN13')).toThrow();
  expect(() => barcodeModules('20000000000AB', 'EAN13')).toThrow();
});

test('Code128 set B: start, data, checksum mod 103, stop', () => {
  // 'A' có giá trị 33; checksum (104 + 33) mod 103 = 34.
  const widths = ['211214', '111323', '131123', '2331112'];
  const expected = widths.map((pattern) => [...pattern].map((width, index) => (index % 2 ? '0' : '1').repeat(+width)).join('')).join('');
  expect(barcodeModules('A', 'Code128')).toBe(expected);
});

test('every Code128 set B pattern is 11 modules wide', () => {
  const printable = Array.from({ length: 95 }, (_, index) => String.fromCharCode(32 + index)).join('');
  expect(barcodeModules(printable, 'Code128')).toHaveLength(11 * (printable.length + 2) + 13);
});

test('SVG shows the digits under the bars', () => {
  const svg = barcodeSvg('2000000000015', 'EAN13');
  expect(svg.match(/<text /g)).toHaveLength(13);
  expect(barcodeSvg('SKU<1>', 'Code128')).toContain('SKU&#60;1&#62;');
});
