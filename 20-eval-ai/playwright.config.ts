import {defineConfig} from '@playwright/test';
export default defineConfig({testDir:'./tests/e2e',fullyParallel:false,timeout:45_000,reporter:'list',use:{baseURL:process.env.EVALAI_BASE_URL??'http://127.0.0.1:8080',browserName:'chromium',launchOptions:{executablePath:process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH??'/usr/bin/chromium',args:['--no-sandbox']}}});
