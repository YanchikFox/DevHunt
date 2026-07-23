// For more info, see https://github.com/storybookjs/eslint-plugin-storybook#configuration-flat-config-format
import storybook from "eslint-plugin-storybook";

import nextCoreWebVitals from "eslint-config-next/core-web-vitals"
import nextTypescript from "eslint-config-next/typescript"

const config = [{
  ignores: [
    "**/node_modules/**",
    "**/.next/**",
    "**/dist/**",
    "**/build/**",
    "**/coverage/**",
    "**/storybook-static/**",

    // Generated/static documentation assets
    "**/docs/**",
  ],
}, ...nextCoreWebVitals, ...nextTypescript, // Repo-wide rule tuning: keep lint actionable and avoid React Compiler-specific rules
// unless we explicitly opt into those patterns.
{
  rules: {
    "@typescript-eslint/no-explicit-any": "warn",

    "react-hooks/set-state-in-effect": "off",
    "react-hooks/refs": "off",
    "react-hooks/static-components": "off",
    "react-hooks/incompatible-library": "off",
  },
}, ...storybook.configs["flat/recommended"]]

export default config
