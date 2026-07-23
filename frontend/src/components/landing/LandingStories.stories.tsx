import type { Meta, StoryObj } from "@storybook/react-vite"
import { LandingStories } from "./LandingStories"

const meta = {
  title: "Landing/Stories",
  component: LandingStories,
  parameters: {
    layout: "fullscreen",
  },
  tags: ["autodocs"],
} satisfies Meta<typeof LandingStories>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {}
