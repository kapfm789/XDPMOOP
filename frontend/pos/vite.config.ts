import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// Cổng theo docs/architecture/context-and-containers.md; CORS ở gateway chỉ cho phép đúng origin này.
// `preview` chạy bản build ở cùng cổng để thử PWA (service worker chỉ đăng ký ở bản build).
export default defineConfig({
  plugins: [react()],
  server: { port: 5174, strictPort: true },
  preview: { port: 5174, strictPort: true },
});
