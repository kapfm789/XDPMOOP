import { searchPosSkus } from '@oism/shared';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

const DEBOUNCE_MS = 200;

const queryOptions = (branchId: string, text: string) => ({
  queryKey: ['pos-skus', branchId, text.trim()],
  queryFn: () => searchPosSkus(branchId, text),
});

// Kết quả tìm theo chữ đang gõ. Máy quét gõ cả mã trong vài chục mili giây, nên chỉ tìm khi chữ đã đứng yên.
export function usePosSearch(branchId: string | undefined, text: string) {
  const [settled, setSettled] = useState(text);
  useEffect(() => {
    const timer = setTimeout(() => setSettled(text), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [text]);

  const queryClient = useQueryClient();
  const results = useQuery({ ...queryOptions(branchId ?? '', settled), enabled: !!branchId, placeholderData: keepPreviousData });

  return {
    skus: results.data ?? [],
    loading: results.isFetching,
    error: results.error,
    // Enter tới trước khi chữ kịp đứng yên (máy quét): tìm ngay với đúng chữ đang có.
    searchNow: (value: string) => queryClient.fetchQuery(queryOptions(branchId ?? '', value)),
    // Tồn khả dụng vừa đổi (bán xong, hoặc backend báo thiếu hàng).
    refresh: () => queryClient.invalidateQueries({ queryKey: ['pos-skus'] }),
  };
}
