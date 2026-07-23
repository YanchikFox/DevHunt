import type { Meta, StoryObj } from "@storybook/react-vite"
import { LandingCTA } from "./LandingCTA"

const meta = {
  title: "Landing/CTA",
  component: LandingCTA,
  parameters: {
    layout: "fullscreen",
  },
  tags: ["autodocs"],
} satisfies Meta<typeof LandingCTA>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {}
