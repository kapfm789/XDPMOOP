import { Link, useParams } from 'react-router-dom';
import { ProductDetailPanel } from '../../features/catalog/ProductDetailPanel';

export function ProductDetailPage() {
  const { id } = useParams();

  return (
    <>
      <Link to="/catalog/products">← Danh sách sản phẩm</Link>
      {/* Đổi sản phẩm thì dựng lại màn hình, không giữ hộp thoại của sản phẩm trước. */}
      {id && <ProductDetailPanel key={id} productId={id} />}
    </>
  );
}
