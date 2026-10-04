import { AuthProvider, RequireRole } from '@oism/shared';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { HomePage } from '../pages/home/HomePage';
import { LoginPage } from '../pages/login/LoginPage';
import { RegisterPage } from '../pages/register/RegisterPage';
import { AdminLayout } from './AdminLayout';
import { ADMIN_ROLES } from './menu';

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
    children: [{ index: true, element: <HomePage /> }],
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
