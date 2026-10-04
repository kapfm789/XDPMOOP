import { AuthProvider, RequireRole } from '@oism/shared';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { BranchesPage } from '../pages/branches/BranchesPage';
import { BrandsPage } from '../pages/catalog/BrandsPage';
import { CategoriesPage } from '../pages/catalog/CategoriesPage';
import { HomePage } from '../pages/home/HomePage';
import { LoginPage } from '../pages/login/LoginPage';
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
