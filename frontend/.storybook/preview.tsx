import type { Preview } from "@storybook/react-vite"
import { NextIntlClientProvider } from "next-intl"
import { ThemeProvider } from "next-themes"
import React from "react"
import "../src/app/globals.css"
import messages from "../messages/en.json"

function IntlDecorator(Story: React.ComponentType) {
  return (
    <NextIntlClientProvider locale="en" messages={messages}>
      <ThemeProvider attribute="class" defaultTheme="light" enableSystem={false}>
        <Story />
      </ThemeProvider>
    </NextIntlClientProvider>
  )
}

const preview: Preview = {
  decorators: [IntlDecorator],
  parameters: {
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
    a11y: {
      config: {
        rules: [
          {
            id: "color-contrast",
            enabled: true,
          },
        ],
      },
    },
  },
}

export default preview
