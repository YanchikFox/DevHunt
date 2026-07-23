import type { Meta, StoryObj } from "@storybook/react-vite"
import { LandingFeatures } from "./LandingFeatures"

const meta = {
  title: "Landing/Features",
  component: LandingFeatures,
  parameters: {
    layout: "fullscreen",
  },
  tags: ["autodocs"],
} satisfies Meta<typeof LandingFeatures>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {}
