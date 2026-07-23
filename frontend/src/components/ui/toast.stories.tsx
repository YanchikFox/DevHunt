import type { Meta, StoryObj } from "@storybook/react-vite";
import { Toast } from "./toast";

const meta = {
  title: "UI/Toast",
  component: Toast,
  parameters: { layout: "centered" },
  tags: ["autodocs"],
} satisfies Meta<typeof Toast>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {},
};
