const config = {
  packageManager: 'npm',
  reporters: ['html', 'clear-text', 'progress', 'json'],
  testRunner: 'vitest',
  checkers: ['typescript'],
  tsconfigFile: 'tsconfig.json',
  coverageAnalysis: 'perTest',
  
  // Fix EISDIR error with Vite - use inPlace sandbox
  tempDirName: '.stryker-tmp',
  inPlace: true,

  // Vitest-specific config to avoid sandbox issues
  vitest: {
    configFile: 'vitest.config.ts',
  },

  // Focus on source files, excluding test files and configs
  mutate: [
    'src/**/*.ts',
    'src/**/*.tsx',
    '!src/**/*.test.ts',
    '!src/**/*.test.tsx',
    '!src/**/*.spec.ts',
    '!src/**/*.spec.tsx',
    '!src/**/*.stories.tsx',
    '!src/**/*.d.ts',
  ],

  // Exclude certain mutation types that produce false positives
  mutator: {
    excludedMutations: [
      'StringLiteral', // CSS classes, i18n keys, etc.
    ],
  },
  
  // Thresholds
  thresholds: {
    high: 80,
    low: 60,
    break: 40,
  },
  
  // Performance
  concurrency: 4,
  timeoutMS: 60000,
  
  // Output
  htmlReporter: {
    fileName: 'reports/mutation/mutation-report.html',
  },
  jsonReporter: {
    fileName: 'reports/mutation/mutation-report.json',
  },
};

export default config;
