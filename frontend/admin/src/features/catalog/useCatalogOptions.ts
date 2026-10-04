import { type CategoryNode, getCategoryTree, listBrands } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';

type TreeOption = { value: string; title: string; children: TreeOption[] };

const toTree = (nodes: CategoryNode[]): TreeOption[] =>
  nodes.map((node) => ({ value: node.id, title: node.name, children: toTree(node.children) }));

const flatten = (nodes: CategoryNode[]): CategoryNode[] => nodes.flatMap((node) => [node, ...flatten(node.children)]);

// Danh mục và thương hiệu của tenant: lựa chọn cho ô chọn, và tên để hiện thay cho id.
export function useCatalogOptions() {
  const categories = useQuery({ queryKey: ['categories'], queryFn: getCategoryTree });
  const brands = useQuery({ queryKey: ['brands'], queryFn: listBrands });
  const categoryNames = new Map(flatten(categories.data ?? []).map((category) => [category.id, category.name]));
  const brandNames = new Map((brands.data ?? []).map((brand) => [brand.id, brand.name]));

  return {
    error: categories.error ?? brands.error,
    categoryTree: toTree(categories.data ?? []),
    brandOptions: (brands.data ?? []).map((brand) => ({ value: brand.id, label: brand.name })),
    categoryName: (id?: string | null) => (id && categoryNames.get(id)) || '',
    brandName: (id?: string | null) => (id && brandNames.get(id)) || '',
  };
}
