import { useParams } from 'react-router-dom';
import { PageHeader } from '../../app/PageHeader';
import { ProductDetailPanel } from '../../features/catalog/ProductDetailPanel';

export function ProductDetailPage() {
  const { id } = useParams();

  // Đổi sản phẩm thì dựng lại màn hình, không giữ hộp thoại của sản phẩm trước.
  return id && <ProductDetailPanel key={id} productId={id} header={(props) => <PageHeader crumb={props.title} {...props} />} />;
}
