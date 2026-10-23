import { expect, test } from 'vitest';
import { bankName, VIETQR_BANKS } from './banks';

test('lists each bank once with a six-digit NAPAS BIN', () => {
  const bins = VIETQR_BANKS.map((bank) => bank.bin);
  expect(new Set(bins).size).toBe(bins.length);
  for (const bin of bins) expect(bin).toMatch(/^970\d{3}$/);
  for (const bank of VIETQR_BANKS) {
    expect(bank.shortName.trim()).not.toBe('');
    expect(bank.name.trim()).not.toBe('');
  }
});

test('names a bank by its BIN and falls back to the BIN itself', () => {
  expect(bankName('970436')).toBe('Vietcombank');
  expect(bankName('970418')).toBe('BIDV');
  expect(bankName('123456')).toBe('123456');
});
