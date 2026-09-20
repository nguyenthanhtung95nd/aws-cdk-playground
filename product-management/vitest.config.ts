import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    globals: true,
    environment: 'node',
    include: ['test/**/*.test.ts'],
    // CDK synth (esbuild bundling, aws-cdk-lib cold start) can exceed the 5s default.
    testTimeout: 30000,
    hookTimeout: 30000,
  },
});
