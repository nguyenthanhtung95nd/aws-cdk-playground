import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Under the Aspire host both values come from the environment; the fallbacks serve a bare `npm run dev`.
const LOCAL_API_GATEWAY = process.env.API_PROXY_TARGET ?? 'http://localhost:5300';
const DEV_SERVER_PORT = Number(process.env.PORT) || 5173;

export default defineConfig({
  plugins: [react()],
  server: {
    port: DEV_SERVER_PORT,
    strictPort: true,
    proxy: {
      '/api': {
        target: LOCAL_API_GATEWAY,
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
});
