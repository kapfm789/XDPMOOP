// Chuỗi VietQR (chuẩn EMVCo của NAPAS) cho chuyển khoản tới một tài khoản kèm số tiền: app ngân hàng nào quét
// cũng tự điền ngân hàng, số tài khoản và số tiền. Sinh ngay trên máy, không gọi dịch vụ ngoài.
const field = (id: string, value: string) => `${id}${value.length.toString().padStart(2, '0')}${value}`;

// CRC-16/CCITT-FALSE: đa thức 0x1021, giá trị đầu 0xFFFF.
export function crc16(text: string): string {
  let crc = 0xffff;
  for (const ch of text) {
    crc ^= ch.charCodeAt(0) << 8;
    for (let i = 0; i < 8; i++) crc = crc & 0x8000 ? ((crc << 1) ^ 0x1021) & 0xffff : (crc << 1) & 0xffff;
  }
  return crc.toString(16).toUpperCase().padStart(4, '0');
}

// bankBin: mã BIN 6 số của ngân hàng nhận (ví dụ 970436 là Vietcombank).
export function vietQrPayload(bankBin: string, accountNo: string, amount: number): string {
  const beneficiary = field('00', bankBin) + field('01', accountNo);
  const body =
    field('00', '01') +
    field('01', '12') +
    field('38', field('00', 'A000000727') + field('01', beneficiary) + field('02', 'QRIBFTTA')) +
    field('53', '704') +
    field('54', String(Math.round(amount))) +
    field('58', 'VN') +
    '6304';
  return body + crc16(body);
}
