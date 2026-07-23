import type { Meta, StoryObj } from "@storybook/react-vite"
import { UserStatsPanel } from "./UserStatsPanel"

const meta = {
  title: "Components/Profile/UserStatsPanel",
  component: UserStatsPanel,
  parameters: {
    layout: "padded",
  },
  tags: ["autodocs"],
  argTypes: {
    isVisible: { control: "boolean" },
  },
} satisfies Meta<typeof UserStatsPanel>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {
  args: {
    isVisible: true,
    stats: {
      followersCount: 48,
      followingCount: 23,
      projectsCount: 5,
      contributionScore: 72,
      communityScore: 55,
      consistencyDaysActiveLast14: 9,
      recentActivityCount: 34,
    },
  },
}

export const HighActivity: Story = {
  args: {
    isVisible: true,
    stats: {
      followersCount: 320,
      followingCount: 150,
      projectsCount: 18,
      contributionScore: 95,
      communityScore: 88,
      consistencyDaysActiveLast14: 14,
      recentActivityCount: 127,
    },
  },
}

export const LowActivity: Story = {
  args: {
    isVisible: true,
    stats: {
      followersCount: 2,
      followingCount: 1,
      projectsCount: 1,
      contributionScore: 12,
      communityScore: 5,
      consistencyDaysActiveLast14: 1,
      recentActivityCount: 3,
    },
  },
}

export const Hidden: Story = {
  args: {
    isVisible: false,
    stats: {
      followersCount: 25,
      followingCount: 10,
      projectsCount: 3,
      contributionScore: 50,
      communityScore: 50,
      consistencyDaysActiveLast14: 7,
      recentActivityCount: 20,
    },
  },
}
