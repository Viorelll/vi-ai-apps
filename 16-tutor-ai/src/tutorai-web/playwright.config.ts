import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "e2e",
  use: {
    baseURL: "http://127.0.0.1:8080",
    headless: true,
    launchOptions: { executablePath: "/usr/bin/chromium" },
  },
  workers: 1,
});
