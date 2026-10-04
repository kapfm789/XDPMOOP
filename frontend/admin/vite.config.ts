import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// Cổng theo docs/architecture/context-and-containers.md; CORS ở gateway chỉ cho phép đúng origin này.
export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true },
});
