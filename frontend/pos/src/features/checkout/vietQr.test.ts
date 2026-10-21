import { expect, test } from 'vitest';
import { crc16, vietQrPayload } from './vietQr';

test('crc16 matches the CRC-16/CCITT-FALSE check value', () => {
  expect(crc16('123456789')).toBe('29B1');
});

test('VietQR payload carries bank, account, amount and a valid checksum', () => {
  const payload = vietQrPayload('970436', '0123456789', 150000);
  expect(payload).toBe(
    '000201010212' +
      '38540010A00000072701240006970436011001234567890208QRIBFTTA' +
      '5303704' +
      '5406150000' +
      '5802VN' +
      '6304' +
      crc16(payload.slice(0, -4)),
  );
});
