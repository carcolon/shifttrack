import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig, type RollupLog, type RollupWarning } from 'vite';

const isIgnorableSignalrWarning = (warning: RollupLog | RollupWarning) =>
  typeof warning.message === 'string' &&
  warning.message.includes('contains an annotation that Rollup cannot interpret') &&
  (warning.id?.includes('@microsoft/signalr') ?? false);

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    rollupOptions: {
      onwarn(warning, warn) {
        if (isIgnorableSignalrWarning(warning)) {
          return;
        }
        warn(warning);
      },
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) {
            return undefined;
          }

          if (id.includes('@microsoft/signalr')) {
            return 'vendor-signalr';
          }

          if (id.includes('gsap')) {
            return 'vendor-gsap';
          }

          if (id.includes('react-router-dom') || id.includes('react-router')) {
            return 'vendor-router';
          }

          if (
            id.includes('react-dom') ||
            id.includes('react/jsx-runtime') ||
            id.includes('react/jsx-dev-runtime') ||
            id.includes('\\react\\') ||
            id.includes('/react/') ||
            id.includes('scheduler')
          ) {
            return 'vendor-react';
          }

          return undefined;
        },
      },
    },
  },
});
