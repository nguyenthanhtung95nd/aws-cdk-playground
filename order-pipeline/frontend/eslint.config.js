import js from '@eslint/js';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist', 'node_modules', 'playwright-report', 'test-results', 'coverage'] },
  js.configs.recommended,
  ...tseslint.configs.recommendedTypeChecked,
  {
    languageOptions: {
      parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname },
    },
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    extends: [jsxA11y.flatConfigs.strict, reactHooks.configs.flat['recommended-latest']],
    languageOptions: { globals: { ...globals.browser } },
    rules: {
      '@typescript-eslint/consistent-type-assertions': [
        'error',
        { assertionStyle: 'never' },
      ],
      // A scrollable region must be focusable or keyboard users cannot scroll it.
      'jsx-a11y/no-noninteractive-tabindex': ['error', { roles: ['tabpanel', 'region'] }],
    },
  },
  {
    files: ['tests/**/*.{ts,tsx}', 'e2e/**/*.ts', '*.config.ts'],
    languageOptions: { globals: { ...globals.node, ...globals.browser } },
  },
  {
    files: ['eslint.config.js'],
    extends: [tseslint.configs.disableTypeChecked],
  },
);
