import { listBranches, useAuth } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';

const STORAGE_KEY = 'oism.pos.branchId';

// Chi nhánh đang bán: Cashier lấy từ token; Owner phải chọn trước khi bán (docs/design/ui/pos.md mục "Quy tắc").
export function usePosBranch() {
  const { user } = useAuth();
  const fixed = user?.branchId ?? undefined;
  const [chosen, setChosen] = useState(() => localStorage.getItem(STORAGE_KEY) ?? undefined);
  // Chỉ người không gắn chi nhánh mới cần danh sách; Cashier không có quyền xem danh sách chi nhánh.
  const branches = useQuery({ queryKey: ['branches', 'active'], queryFn: () => listBranches(true), enabled: !!user && !fixed });
  const options = (branches.data ?? []).map((branch) => ({ value: branch.id, label: `${branch.code} · ${branch.name}` }));

  return {
    // Lựa chọn đã lưu chỉ dùng khi nó còn là một chi nhánh đang hoạt động của tenant này.
    branchId: fixed ?? options.find((option) => option.value === chosen)?.value,
    canChoose: !fixed,
    options,
    error: branches.error,
    choose: (branchId: string) => {
      localStorage.setItem(STORAGE_KEY, branchId);
      setChosen(branchId);
    },
  };
}
