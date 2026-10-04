import { listBranches } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';

// Chi nhánh của tenant: lựa chọn cho ô chọn, và tên để hiện thay cho id.
export function useBranchOptions() {
  const branches = useQuery({ queryKey: ['branches'], queryFn: () => listBranches() });
  const all = (branches.data ?? []).map((branch) => ({ value: branch.id, label: `${branch.code} · ${branch.name}`, isActive: branch.isActive }));
  const names = new Map(all.map((branch) => [branch.value, branch.label]));

  return {
    error: branches.error,
    options: all,
    // Chi nhánh đã tắt không nhận chứng từ mới.
    activeOptions: all.filter((branch) => branch.isActive),
    name: (id: string) => names.get(id) ?? '',
  };
}
