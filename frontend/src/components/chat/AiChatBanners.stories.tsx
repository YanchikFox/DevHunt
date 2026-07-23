import type { Meta, StoryObj } from "@storybook/react-vite"
import { AiDisclaimerBanner, ProjectContextBanner, AgentModeBanner } from "./AiChatBanners"

const mockT = (key: string): string => {
  const translations: Record<string, string> = {
    "ai.disclaimer": "AI-generated content may contain errors. Always verify important information.",
    "ai.contextLoaded": "Project context loaded",
    "ai.membersShort": "members",
    "ai.tasksShort": "tasks",
    "ai.agentMode": "Agent mode enabled — AI can execute actions on your behalf.",
    "ai.guestMode": "Guest mode — read-only access, no actions will be performed.",
  }
  return translations[key] || key
}

// -- AiDisclaimerBanner --

const disclaimerMeta = {
  title: "Components/Chat/AiDisclaimerBanner",
  component: AiDisclaimerBanner,
  parameters: { layout: "padded" },
  tags: ["autodocs"],
} satisfies Meta<typeof AiDisclaimerBanner>

export default disclaimerMeta
type DisclaimerStory = StoryObj<typeof disclaimerMeta>

export const Default: DisclaimerStory = {
  args: { t: mockT },
}

// -- ProjectContextBanner (as named export story) --

export const WithProjectContext: DisclaimerStory = {
  args: { t: mockT },
  render: (args) => (
    <ProjectContextBanner
      t={args.t}
      projectContext={{
        team: { total: 5, members: [] },
        tasks: { total: 12, items: [] },
      } as never}
    />
  ),
}

// -- AgentModeBanner stories --

export const AgentModeEnabled: DisclaimerStory = {
  args: { t: mockT },
  render: () => <AgentModeBanner t={mockT} agentEnabled={true} />,
}

export const GuestMode: DisclaimerStory = {
  args: { t: mockT },
  render: () => <AgentModeBanner t={mockT} agentEnabled={false} />,
}
