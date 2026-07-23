import type { Meta, StoryObj } from "@storybook/react-vite";
import { StatsCard } from "./stats-card";
import { TrendingUp } from "lucide-react";

const meta = {
  title: "UI/StatsCard",
  component: StatsCard,
  parameters: { layout: "centered" },
  tags: ["autodocs"],
} satisfies Meta<typeof StatsCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    title: "Total Users",
    value: 1234,
    icon: TrendingUp,
  },
};
