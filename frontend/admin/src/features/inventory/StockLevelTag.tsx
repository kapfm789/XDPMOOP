import { StatusTag } from '../common/StatusTag';

// Mức tồn theo tồn khả dụng và ngưỡng mà backend trả về: hết hàng đỏ, chạm ngưỡng cam, còn lại xanh lá. Cảnh báo chính
// thức vẫn là danh sách của insights; tag này chỉ để đọc nhanh trên bảng.
export function StockLevelTag({ available, threshold }: { available: number; threshold?: number | null }) {
  if (available <= 0) return <StatusTag tone="error">Hết hàng</StatusTag>;
  if (threshold !== undefined && threshold !== null && available <= threshold) return <StatusTag tone="pending">Sắp hết</StatusTag>;
  return <StatusTag tone="success">Đủ hàng</StatusTag>;
}
