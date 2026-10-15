// SKU chưa tới insights qua SkuUpserted thì chưa có mã và tên: hiện id.
export const skuLabel = (row: { skuId: string; skuCode: string | null; name: string | null }) =>
  row.skuCode ? `${row.skuCode} · ${row.name}` : row.skuId;
