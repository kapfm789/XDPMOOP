import { BarcodeDetector, prepareZXingModule } from 'barcode-detector/ponyfill';
import wasmUrl from 'zxing-wasm/reader/zxing_reader.wasm?url';

// Bộ giải mã chạy bằng WebAssembly; file .wasm đi cùng bản build thay vì tải từ CDN mặc định của thư viện.
prepareZXingModule({
  overrides: { locateFile: (path: string, prefix: string) => (path.endsWith('.wasm') ? wasmUrl : prefix + path) },
});

// Hai loại mã vạch mà catalog gán cho SKU: docs/design/data-model/catalog.md.
const detector = new BarcodeDetector({ formats: ['ean_13', 'code_128'] });

const REPEAT_MS = 1500;
const FRAME_MS = 200;

// Mã nằm yên trong khung bị đọc ở mọi khung hình: chỉ tính lại khi nó đã vắng khỏi khung ít nhất REPEAT_MS.
export function freshScan(lastSeen: Map<string, number>, code: string, now: number): boolean {
  const previous = lastSeen.get(code);
  lastSeen.set(code, now);
  return previous === undefined || now - previous >= REPEAT_MS;
}

// Quét video tới khi signal bị hủy; mỗi mã mới gọi onCode một lần.
export async function scanVideo(video: HTMLVideoElement, onCode: (code: string) => void, signal: AbortSignal) {
  const lastSeen = new Map<string, number>();
  while (!signal.aborted) {
    for (const barcode of await detector.detect(video)) {
      if (!signal.aborted && freshScan(lastSeen, barcode.rawValue, Date.now())) onCode(barcode.rawValue);
    }
    await new Promise((resolve) => setTimeout(resolve, FRAME_MS));
  }
}

// Mã đầu tiên đọc được trong ảnh; ảnh không có mã thì undefined.
export async function readImage(image: Blob): Promise<string | undefined> {
  return (await detector.detect(image))[0]?.rawValue;
}
