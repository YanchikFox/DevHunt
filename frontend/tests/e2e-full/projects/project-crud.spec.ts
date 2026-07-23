import { test, expect } from "@playwright/test"
import { ProjectsListPage } from "../page-objects/projects/projects-list.page"
import { ProjectDetailPage } from "../page-objects/projects/project-detail.page"
import { login } from "../helpers"
import { generateProjectData } from "../fixtures/test-projects"

test.describe("Projects - CRUD Operations", () => {
  test.setTimeout(7 * 60_000)

  let projectsPage: ProjectsListPage
  let projectDetailPage: ProjectDetailPage

  test.beforeEach(async ({ page }) => {
    await login(page)
    projectsPage = new ProjectsListPage(page)
    projectDetailPage = new ProjectDetailPage(page)
  })

  test("should create project with minimum fields (title and description)", async ({
    page,
  }) => {
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    const projectData = generateProjectData({ prefix: "Minimal" })
    await projectsPage.createDialog.fillForm({
      title: projectData.title,
      description: projectData.description,
    })
    await projectsPage.createDialog.submit()

    await projectsPage.expectProjectVisible(projectData.title)
  })

  test("should create project with all fields (technologies, roles, visibility)", async ({
    page,
  }) => {
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    const projectData = generateProjectData({
      prefix: "FullFields",
      technologies: ["React", "Node.js", "PostgreSQL"],
      roles: ["Frontend Developer", "Backend Developer"],
      visibility: "public",
    })

    await projectsPage.createDialog.fillForm(projectData)
    await projectsPage.createDialog.submit()

    await projectsPage.expectProjectVisible(projectData.title)
  })

  test("should create private project", async ({ page }) => {
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    const projectData = generateProjectData({
      prefix: "Private",
      visibility: "private",
    })

    await projectsPage.createDialog.fillForm(projectData)
    await projectsPage.createDialog.submit()

    await projectsPage.expectProjectVisible(projectData.title)
  })

  test("should navigate to project details page", async ({ page }) => {
    // First create a project
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    const projectData = generateProjectData({ prefix: "ViewDetails" })
    await projectsPage.createDialog.fillForm(projectData)
    await projectsPage.createDialog.submit()

    // Click to view details
    await projectsPage.clickProject(projectData.title)

    await expect(page).toHaveURL(/\/dashboard\/projects\//)
    await expect(projectDetailPage.projectTitle).toContainText(projectData.title)
  })

  test("should show validation error for title that is too short", async ({ page }) => {
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    await projectsPage.createDialog.titleInput.fill("AB") // Too short (< 3 chars)
    await projectsPage.createDialog.descriptionInput.fill(
      "This is a valid description that meets the minimum length requirement."
    )
    await projectsPage.createDialog.clickCreate()

    // Should show validation error and dialog should remain open
    await expect(projectsPage.createDialog.dialog).toBeVisible()
  })

  test("should show validation error for description that is too short", async ({
    page,
  }) => {
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    await projectsPage.createDialog.titleInput.fill("Valid Project Title")
    await projectsPage.createDialog.descriptionInput.fill("Short") // Too short (< 10 chars)
    await projectsPage.createDialog.clickCreate()

    // Should show validation error and dialog should remain open
    await expect(projectsPage.createDialog.dialog).toBeVisible()
  })

  test("should cancel project creation and close dialog", async ({ page }) => {
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    // Fill some data
    await projectsPage.createDialog.titleInput.fill("Should Not Be Created")
    await projectsPage.createDialog.descriptionInput.fill(
      "This project should not be created because we will cancel."
    )

    // Cancel
    await projectsPage.createDialog.cancel()

    // Dialog should be closed
    await expect(projectsPage.createDialog.dialog).toBeHidden()

    // Project should not appear in list
    await projectsPage.expectProjectNotVisible("Should Not Be Created")
  })
})
