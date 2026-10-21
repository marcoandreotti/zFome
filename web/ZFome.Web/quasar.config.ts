import { defineConfig } from '#q-app/wrappers'

export default defineConfig(() => ({
  boot: ['appearance', 'http'],
  css: ['app.scss'],
  extras: ['material-icons'],
  build: {
    target: {
      browser: ['es2022'],
      node: 'node22'
    },
    vueRouterMode: 'history'
  },
  devServer: {
    open: false,
    port: 9000,
    watch: {
      usePolling: process.env.VITE_USE_POLLING === 'true'
    },
    proxy: {
      '/api': {
        target: process.env.API_PROXY_TARGET ?? 'http://localhost:8080',
        changeOrigin: true
      },
      '/hubs': {
        target: process.env.API_PROXY_TARGET ?? 'http://localhost:8080',
        changeOrigin: true,
        ws: true
      }
    }
  },
  framework: {
    iconSet: 'material-icons',
    config: {
      brand: {
        primary: '#f97316',
        secondary: '#1f2937',
        accent: '#ea580c',
        positive: '#15803d',
        negative: '#dc2626',
        warning: '#92400e',
        info: '#2563eb'
      }
    },
    plugins: ['Dark']
  }
}))
