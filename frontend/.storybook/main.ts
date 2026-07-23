// This file has been automatically migrated to valid ESM format by Storybook.

import type { StorybookConfig } from "@storybook/react-vite";

const config: StorybookConfig = {
  stories: ["../src/**/*.stories.@(js|jsx|ts|tsx|mdx)"],
  addons: ["@storybook/addon-a11y", "@storybook/addon-docs"],
  framework: {
    name: "@storybook/react-vite",
    options: {},
  },
  viteFinal: async (config) => {
    config.resolve = config.resolve || {};
    config.resolve.alias = {
      ...config.resolve.alias,
      "@": new URL("../src", import.meta.url).pathname,
      "@/i18n/routing": new URL("mocks/i18n-routing.tsx", import.meta.url).pathname,
    }
    return config
  },
}

export default config
