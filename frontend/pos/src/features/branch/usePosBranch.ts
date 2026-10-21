import { type Branch, getBranch, listBranches, useAuth } from '@oism/shared';
import { useQuery } from '@tanstack/react-query';
import { createContext, createElement, type ReactNode, useContext, useState } from 'react';

const STORAGE_KEY = 'oism.pos.branchId';

type PosBranch = {
  branchId: string | undefined;
  // Chi nhánh đang bán, kèm tài khoản nhận chuyển khoản cho mã VietQR (W5-12).
  current: Branch | undefined;
  canChoose: boolean;
  options: { value: string; label: string }[];
  error: Error | null;
  choose: (branchId: string) => void;
};

function useBranchState(): PosBranch {
  const { user } = useAuth();
  const fixed = user?.branchId ?? undefined;
  const [chosen, setChosen] = useState(() => localStorage.getItem(STORAGE_KEY) ?? undefined);
  // Chỉ người không gắn chi nhánh mới cần danh sách; Cashier không có quyền xem danh sách chi nhánh.
  const branches = useQuery({ queryKey: ['branches', 'active'], queryFn: () => listBranches(true), enabled: !!user && !fixed });
  const options = (branches.data ?? []).map((branch) => ({ value: branch.id, label: `${branch.code} · ${branch.name}` }));
  // Lựa chọn đã lưu chỉ dùng khi nó còn là một chi nhánh đang hoạt động của tenant này.
  const branchId = fixed ?? options.find((option) => option.value === chosen)?.value;
  // Cashier không xem được danh sách nhưng đọc được chi nhánh của mình theo id.
  const current = useQuery({ queryKey: ['branch', branchId], queryFn: () => getBranch(branchId!), enabled: !!branchId });

  return {
    branchId,
    current: current.data,
    canChoose: !fixed,
    options,
    error: branches.error ?? current.error,
    choose: (branchId: string) => {
      localStorage.setItem(STORAGE_KEY, branchId);
      setChosen(branchId);
    },
  };
}

const PosBranchContext = createContext<PosBranch | null>(null);

// Một chi nhánh đang bán cho cả thanh trên, màn hình bán hàng và thông báo, để đổi ở thanh trên là mọi nơi thấy ngay.
export function PosBranchProvider({ children }: { children: ReactNode }) {
  return createElement(PosBranchContext.Provider, { value: useBranchState() }, children);
}

// Chi nhánh đang bán: Cashier lấy từ token; Owner phải chọn trước khi bán (docs/design/ui/pos.md mục "Quy tắc").
export function usePosBranch(): PosBranch {
  const branch = useContext(PosBranchContext);
  if (!branch) throw new Error('usePosBranch must be used inside PosBranchProvider');
  return branch;
}
