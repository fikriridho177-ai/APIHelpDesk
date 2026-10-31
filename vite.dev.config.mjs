import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],

  server: {
    host: "192.168.9.23",
    port: 5173,
    strictPort: true,

    proxy: {
      "/api": {
        target: "http://192.168.9.23:8181",
        changeOrigin: true,
      },

      "/hubs": {
        target: "http://192.168.9.23:8181",
        changeOrigin: true,
        ws: true,
      },
    },
  },
});
