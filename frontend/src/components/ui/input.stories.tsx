import type { Meta, StoryObj } from "@storybook/react-vite"
import { Input } from "./input"

const meta = {
  title: "UI/Input",
  component: Input,
  parameters: {
    layout: "centered",
  },
  tags: ["autodocs"],
} satisfies Meta<typeof Input>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {
  args: {
    placeholder: "Enter text...",
  },
}

export const WithLabel: Story = {
  render: () => (
    <div className="space-y-2">
      <label htmlFor="email" className="text-sm font-medium">
        Email
      </label>
      <Input id="email" type="email" placeholder="email@example.com" />
    </div>
  ),
}

export const Disabled: Story = {
  args: {
    disabled: true,
    placeholder: "Disabled input",
  },
}

export const WithError: Story = {
  render: () => (
    <div className="space-y-2">
      <Input aria-invalid="true" placeholder="Input with error" />
      <p className="text-sm text-destructive">This field is required</p>
    </div>
  ),
}
