import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// bank-teller dev server runs on 5174 to avoid clashing with bank-client (5173)
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174,
    host: true,
  },
});
