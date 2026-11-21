import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './specs', fullyParallel: false, workers: 1,
  outputDir: '../../coverage/playwright/artifacts',
  reporter: [['list'], ['html', {outputFolder:'../../coverage/playwright/report',open:'never'}]],
  use: { baseURL: process.env.E2E_BASE_URL || 'http://localhost:8080',
    screenshot:'only-on-failure', trace:'retain-on-failure' }
});
