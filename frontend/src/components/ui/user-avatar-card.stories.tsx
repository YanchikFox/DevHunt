import type { Meta, StoryObj } from "@storybook/react-vite";
import { UserAvatarCard } from "./user-avatar-card";

const meta = {
  title: "UI/UserAvatarCard",
  component: UserAvatarCard,
  parameters: { layout: "centered" },
  tags: ["autodocs"],
} satisfies Meta<typeof UserAvatarCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: { name: "John Doe" },
};
