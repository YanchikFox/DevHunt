import type { Meta, StoryObj } from "@storybook/react-vite";
import { MermaidDiagram } from "./MermaidDiagram";

const meta = {
  title: "UI/MermaidDiagram",
  component: MermaidDiagram,
  parameters: { layout: "centered" },
  tags: ["autodocs"],
} satisfies Meta<typeof MermaidDiagram>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: { code: "flowchart TD\n    A --> B" },
};
