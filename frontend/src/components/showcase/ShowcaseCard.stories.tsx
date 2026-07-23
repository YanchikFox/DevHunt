import type { Meta, StoryObj } from "@storybook/react-vite"
import { ShowcaseCard } from "./ShowcaseCard"
import type { ShowcaseListItem } from "@/lib/api/queries/showcase"

const baseMock: ShowcaseListItem = {
  id: "sc-1",
  projectId: "proj-1",
  projectTitle: "WeatherKit",
  summary: "A modern weather dashboard with real-time alerts and beautiful data visualizations.",
  demoUrl: "https://example.com/demo",
  screenshotsJson: JSON.stringify(["https://picsum.photos/seed/weather/600/400"]),
  repositoryUrl: "https://github.com/example/weatherkit",
  publishedAt: "2026-02-15T10:00:00Z",
  featured: false,
  viewsCount: 342,
  likesCount: 28,
}

const meta = {
  title: "Components/Showcase/ShowcaseCard",
  component: ShowcaseCard,
  parameters: {
    layout: "centered",
  },
  tags: ["autodocs"],
  decorators: [
    (Story) => (
      <div style={{ width: 380 }}>
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof ShowcaseCard>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {
  args: {
    showcase: baseMock,
  },
}

export const Featured: Story = {
  args: {
    showcase: { ...baseMock, featured: true, projectTitle: "StarProject AI" },
  },
}

export const NoScreenshot: Story = {
  args: {
    showcase: {
      ...baseMock,
      screenshotsJson: null,
      projectTitle: "CLI Toolkit",
      summary: "A terminal-first developer toolkit with zero dependencies.",
    },
  },
}

export const MinimalLinks: Story = {
  args: {
    showcase: {
      ...baseMock,
      demoUrl: null,
      repositoryUrl: null,
      projectTitle: "Private Project",
    },
  },
}

export const HighEngagement: Story = {
  args: {
    showcase: {
      ...baseMock,
      featured: true,
      viewsCount: 12840,
      likesCount: 1053,
      projectTitle: "DevHunt Platform",
      summary: "The open-source project discovery platform that connects developers worldwide.",
    },
  },
}
