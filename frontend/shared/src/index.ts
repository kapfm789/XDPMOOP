export { api, ApiError } from './api/client';
export { errorMessage } from './api/errors';
export { registerTenant } from './api/identity';
export { AuthProvider, useAuth } from './auth/AuthProvider';
export { LoginForm } from './auth/LoginForm';
export { RequireRole } from './auth/RequireRole';
export { canAccess, visibleFor } from './auth/roles';
export type * from './types/identity';
