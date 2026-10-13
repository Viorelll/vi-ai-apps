import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  timeout: 45000,
  use: {
    baseURL: process.env.BUILDBUDGET_BASE_URL ?? "http://127.0.0.1:8080",
    headless: true,
    launchOptions: {
      executablePath: process.env.CHROMIUM_PATH,
      args: ["--no-sandbox"],
    },
    screenshot: "only-on-failure",
  },
  reporter: [["list"]],
});
