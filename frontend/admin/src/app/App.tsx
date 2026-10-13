import { AuthProvider, RequireRole } from '@oism/shared';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { BranchesPage } from '../pages/branches/BranchesPage';
import { BrandsPage } from '../pages/catalog/BrandsPage';
import { CategoriesPage } from '../pages/catalog/CategoriesPage';
import { ProductDetailPage } from '../pages/catalog/ProductDetailPage';
import { ProductsPage } from '../pages/catalog/ProductsPage';
import { ChannelPage } from '../pages/channel/ChannelPage';
import { HomePage } from '../pages/home/HomePage';
import { LedgerPage } from '../pages/inventory/LedgerPage';
import { ReceiptsPage } from '../pages/inventory/ReceiptsPage';
import { StockPage } from '../pages/inventory/StockPage';
import { LoginPage } from '../pages/login/LoginPage';
import { NewOrderPage } from '../pages/orders/NewOrderPage';
import { OrderDetailPage } from '../pages/orders/OrderDetailPage';
import { OrdersPage } from '../pages/orders/OrdersPage';
import { RegisterPage } from '../pages/register/RegisterPage';
import { UsersPage } from '../pages/users/UsersPage';
import { AdminLayout } from './AdminLayout';
import { ADMIN_ROLES, OWNER_ONLY } from './menu';

const queryClient = new QueryClient();

// Route và vai trò: docs/design/ui/admin.md. Route chỉ một vai trò được vào thì bọc thêm RequireRole của riêng nó.
const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  {
    element: (
      <RequireRole roles={ADMIN_ROLES}>
        <AdminLayout />
      </RequireRole>
    ),
    children: [
      { index: true, element: <HomePage /> },
      {
        path: 'branches',
        element: (
          <RequireRole roles={OWNER_ONLY}>
            <BranchesPage />
          </RequireRole>
        ),
      },
      {
        path: 'users',
        element: (
          <RequireRole roles={OWNER_ONLY}>
            <UsersPage />
          </RequireRole>
        ),
      },
      { path: 'catalog/categories', element: <CategoriesPage /> },
      { path: 'catalog/brands', element: <BrandsPage /> },
      { path: 'catalog/products', element: <ProductsPage /> },
      { path: 'catalog/products/:id', element: <ProductDetailPage /> },
      { path: 'inventory/stock', element: <StockPage /> },
      { path: 'inventory/receipts', element: <ReceiptsPage /> },
      { path: 'inventory/ledger', element: <LedgerPage /> },
      { path: 'orders', element: <OrdersPage /> },
      { path: 'orders/new', element: <NewOrderPage /> },
      { path: 'orders/:id', element: <OrderDetailPage /> },
      {
        path: 'channel',
        element: (
          <RequireRole roles={OWNER_ONLY}>
            <ChannelPage />
          </RequireRole>
        ),
      },
    ],
  },
]);

export function App() {
  return (
    <ConfigProvider locale={viVN}>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </QueryClientProvider>
    </ConfigProvider>
  );
}
