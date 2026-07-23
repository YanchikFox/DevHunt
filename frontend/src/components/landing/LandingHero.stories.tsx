import type { Meta, StoryObj } from "@storybook/react-vite"
import { LandingHero } from "./LandingHero"

const meta = {
  title: "Landing/Hero",
  component: LandingHero,
  parameters: {
    layout: "fullscreen",
  },
  tags: ["autodocs"],
} satisfies Meta<typeof LandingHero>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {}
