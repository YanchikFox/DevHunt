import type { Meta, StoryObj } from "@storybook/react-vite";
import { OnlineIndicator } from "./online-indicator";

const meta = {
  title: "UI/OnlineIndicator",
  component: OnlineIndicator,
  parameters: { layout: "centered" },
  tags: ["autodocs"],
} satisfies Meta<typeof OnlineIndicator>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: { isOnline: true },
};
