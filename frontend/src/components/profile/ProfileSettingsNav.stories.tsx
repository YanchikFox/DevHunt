import type { Meta, StoryObj } from "@storybook/react-vite"
import { ProfileSettingsNav } from "./ProfileSettingsNav"

const meta = {
  title: "Components/ProfileSettingsNav",
  component: ProfileSettingsNav,
  parameters: {
    layout: "centered",
  },
  tags: ["autodocs"],
} satisfies Meta<typeof ProfileSettingsNav>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {}
