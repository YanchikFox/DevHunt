import type { Meta, StoryObj } from "@storybook/react-vite"
import { LandingAISection } from "./LandingAISection"

const meta = {
  title: "Landing/AISection",
  component: LandingAISection,
  parameters: {
    layout: "fullscreen",
  },
  tags: ["autodocs"],
} satisfies Meta<typeof LandingAISection>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {}
