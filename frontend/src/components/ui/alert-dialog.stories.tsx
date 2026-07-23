import type { Meta, StoryObj } from "@storybook/react-vite";
import { AlertDialog } from "./alert-dialog";

const meta = {
  title: "UI/AlertDialog",
  component: AlertDialog,
  parameters: { layout: "centered" },
  tags: ["autodocs"],
} satisfies Meta<typeof AlertDialog>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {},
};
