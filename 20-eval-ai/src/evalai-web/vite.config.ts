import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
export default defineConfig({
  plugins: [react()],
  server: { host: "0.0.0.0", proxy: { "/api": "http://localhost:5100" } },
  test: {
    environment: "jsdom",
    setupFiles: "./src/test-setup.ts",
    exclude: ["**/node_modules/**", "**/dist/**", "tests/e2e/**"],
  },
});
