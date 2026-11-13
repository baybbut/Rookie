import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// React islands: build ra MỘT file wwwroot/js/islands/islands.js, được _Layout.cshtml nhúng bằng <script type="module">.
// Sửa code trong client/src xong chạy `npm run build` (hoặc `npm run watch` khi đang phát triển) rồi tải lại trang.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../wwwroot/js/islands',
    emptyOutDir: true,
    rollupOptions: { input: 'src/main.jsx', output: { entryFileNames: 'islands.js', chunkFileNames: '[name].js', assetFileNames: '[name][extname]' } },
  },
});
