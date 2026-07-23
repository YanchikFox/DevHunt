import type { Meta, StoryObj } from "@storybook/react-vite"
import { LoadingSkeleton, LoadingSpinner, LoadingPage, LoadingCard } from "./loading"

const meta = {
  title: "UI/Loading",
  component: LoadingSkeleton,
  parameters: { layout: "padded" },
  tags: ["autodocs"],
} satisfies Meta<typeof LoadingSkeleton>

export default meta
type Story = StoryObj<typeof meta>

export const SkeletonDefault: Story = {
  args: { count: 3 },
}

export const SkeletonSingle: Story = {
  args: { count: 1 },
}

export const Spinner: StoryObj<typeof LoadingSpinner> = {
  render: () => <LoadingSpinner className="h-8 w-8" />,
}

export const Card: StoryObj<typeof LoadingCard> = {
  render: () => <LoadingCard />,
}

export const FullPage: StoryObj<typeof LoadingPage> = {
  parameters: { layout: "fullscreen" },
  render: () => <LoadingPage />,
}
