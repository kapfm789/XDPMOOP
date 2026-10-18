import { AuthProvider, RequireRole, type Role } from '@oism/shared';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { LoginPage } from '../pages/login/LoginPage';
import { ReceiptPage } from '../pages/receipt/ReceiptPage';
import { SalePage } from '../pages/sale/SalePage';
import { PosLayout } from './PosLayout';
import { POS_THEME } from './theme';

// Vai trò được vào POS: docs/design/ui/pos.md.
const POS_ROLES: readonly Role[] = ['Cashier', 'Owner'];

const queryClient = new QueryClient();

const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    element: (
      <RequireRole roles={POS_ROLES}>
        <PosLayout />
      </RequireRole>
    ),
    children: [
      { index: true, element: <SalePage /> },
      { path: 'receipt/:orderId', element: <ReceiptPage /> },
    ],
  },
]);

export function App() {
  return (
    // Cỡ lớn để vùng chạm tối thiểu 44 px; màu và font theo theme chung với admin.
    <ConfigProvider locale={viVN} componentSize="large" theme={POS_THEME}>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </QueryClientProvider>
    </ConfigProvider>
  );
}
