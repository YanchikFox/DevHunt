import type { Meta, StoryObj } from "@storybook/react-vite";
import { Table } from "./table";

const meta = {
  title: "UI/Table",
  component: Table,
  parameters: { layout: "centered" },
  tags: ["autodocs"],
} satisfies Meta<typeof Table>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {},
};
