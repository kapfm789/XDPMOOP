export {
  addBarcode,
  addSku,
  createBrand,
  createCategory,
  createProduct,
  deleteCategory,
  getCategoryTree,
  getProduct,
  listBrands,
  listProducts,
  removeBarcode,
  setPrices,
  updateBrand,
  updateCategory,
  updateProduct,
  updateSku,
} from './api/catalog';
export { createChannelShop, listChannelShops, listWebhookEvents, setChannelShopActive, simulateBurst } from './api/channel';
export { api, ApiError } from './api/client';
export {
  confirmPurchaseReceipt,
  createOrder,
  createPurchaseReceipt,
  createSupplier,
  createTransfer,
  deleteTransfer,
  getOrder,
  listLedger,
  listOrders,
  listPurchaseReceipts,
  listStock,
  listStocktakes,
  listSuppliers,
  listTransfers,
  openStocktake,
  posCheckout,
  postStocktake,
  searchPosSkus,
  setStockThreshold,
  setStocktakeCounts,
  transitionOrder,
  transitionTransfer,
  updatePurchaseReceipt,
} from './api/core';
export { errorMessage } from './api/errors';
export {
  getGrossProfit,
  getInventoryValue,
  getReorderSuggestions,
  listLowStockAlerts,
  listSlowMovers,
  listTopSellers,
  runForecast,
} from './api/insights';
export {
  createBranch,
  createUser,
  getBranch,
  getMe,
  listBranches,
  listUsers,
  registerTenant,
  setBranchActive,
  updateBranch,
  updateUser,
} from './api/identity';
export { AuthProvider, useAuth } from './auth/AuthProvider';
export { LoginForm } from './auth/LoginForm';
export { RequireRole } from './auth/RequireRole';
export { canAccess, visibleFor } from './auth/roles';
export { bankName, VIETQR_BANKS } from './banks';
export { formatDate, formatDateTime, formatMoney, formatNumber, formatPercent, vietnamDate } from './format';
export { playChime } from './realtime/chime';
export { type NotificationHandlers, sendRemoteScan, useNotifications, useRemoteScan } from './realtime/useNotifications';
export { useOnline } from './realtime/useOnline';
// Bộ đọc mã vạch kèm file .wasm khoảng 1 MB: chỉ tải khi cần đọc mã, không vào bundle chính.
export const loadBarcodeReader = () => import('./scan/detector');
export type * from './types/catalog';
export type * from './types/channel';
export type * from './types/common';
export type * from './types/core';
export type * from './types/identity';
export type * from './types/insights';
