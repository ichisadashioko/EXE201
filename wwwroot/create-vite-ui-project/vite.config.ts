import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import cssInjectedByJsPlugin from 'vite-plugin-css-injected-by-js'
import tailwindcss from '@tailwindcss/vite'
// import path from 'path'
// import { fileURLToPath } from 'url'
// const __dirname = path.dirname(fileURLToPath(import.meta.url))

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  return {
    plugins: [
      react(),
      cssInjectedByJsPlugin(),
      tailwindcss(),
    ],
    build: {
      minify: (mode !== 'development'),
      // sourcemap: (mode === 'development') ? 'inline' : false,
      outDir: '../',
      emptyOutDir: false,
      rollupOptions: {
        // input: path.resolve(__dirname, 'src/main.tsx'),
        input: 'src/main.tsx',
        output: {
          // manualChunks: () => 'app_bundle.js',
          manualChunks: undefined,
          entryFileNames: 'app_bundle.js',
          chunkFileNames: 'app_bundle.js',
          assetFileNames: 'assets/[name][extname]',
        },
      },
      cssCodeSplit: false,
    },
  }
})
