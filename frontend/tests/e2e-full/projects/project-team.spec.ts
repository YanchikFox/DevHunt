import { test, expect } from "@playwright/test"
import { ProjectsListPage } from "../page-objects/projects/projects-list.page"
import { ProjectDetailPage } from "../page-objects/projects/project-detail.page"
import { login } from "../helpers"
import { generateProjectData } from "../fixtures/test-projects"

test.describe("Projects - Team Management", () => {
  test.setTimeout(7 * 60_000)

  let projectsPage: ProjectsListPage
  let projectDetailPage: ProjectDetailPage

  test.beforeEach(async ({ page }) => {
    await login(page)
    projectsPage = new ProjectsListPage(page)
    projectDetailPage = new ProjectDetailPage(page)
  })

  test("should display team tab with owner/leader", async ({ page }) => {
    // Create a project
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    const projectData = generateProjectData({ prefix: "TeamTest" })
    await projectsPage.createDialog.fillForm(projectData)
    await projectsPage.createDialog.submit()
    await projectsPage.clickProject(projectData.title)

    // Go to Team tab and verify owner is visible
    await projectDetailPage.expectOwnerVisible()
  })

  test("should show invite button for project owner", async ({ page }) => {
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    const projectData = generateProjectData({ prefix: "InviteTest" })
    await projectsPage.createDialog.fillForm(projectData)
    await projectsPage.createDialog.submit()
    await projectsPage.clickProject(projectData.title)

    await projectDetailPage.switchToTab("team")
    await expect(projectDetailPage.inviteButton).toBeVisible({ timeout: 10_000 })
  })

  test("should open invite team member dialog", async ({ page }) => {
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    const projectData = generateProjectData({ prefix: "InviteDialogTest" })
    await projectsPage.createDialog.fillForm(projectData)
    await projectsPage.createDialog.submit()
    await projectsPage.clickProject(projectData.title)

    await projectDetailPage.openInviteDialog()

    // Invite dialog should be visible
    await expect(
      page.getByRole("dialog", { name: /invite/i })
    ).toBeVisible()
  })
})
