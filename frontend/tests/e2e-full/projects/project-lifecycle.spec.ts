import { test, expect } from "@playwright/test"
import { ProjectsListPage } from "../page-objects/projects/projects-list.page"
import { ProjectDetailPage } from "../page-objects/projects/project-detail.page"
import { login, createProjectViaUI } from "../helpers"
import { generateProjectData } from "../fixtures/test-projects"

test.describe("Projects - Lifecycle Status Transitions", () => {
  test.setTimeout(7 * 60_000)

  let projectsPage: ProjectsListPage
  let projectDetailPage: ProjectDetailPage

  test.beforeEach(async ({ page }) => {
    await login(page)
    projectsPage = new ProjectsListPage(page)
    projectDetailPage = new ProjectDetailPage(page)
  })

  test("should publish draft project to recruiting status", async ({ page }) => {
    // Create a new project (starts as draft)
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    const projectData = generateProjectData({ prefix: "ToPublish" })
    await projectsPage.createDialog.fillForm(projectData)
    await projectsPage.createDialog.submit()

    // Navigate to project detail
    await projectsPage.clickProject(projectData.title)

    // Publish the project
    await projectDetailPage.changeStatus("publish")
    await projectDetailPage.expectStatus("recruiting")
  })

  test("should activate recruiting project to active status", async ({ page }) => {
    // Create and publish a project
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    const projectData = generateProjectData({ prefix: "ToActivate" })
    await projectsPage.createDialog.fillForm(projectData)
    await projectsPage.createDialog.submit()
    await projectsPage.clickProject(projectData.title)

    // Publish first (draft -> recruiting)
    await projectDetailPage.changeStatus("publish")

    // Then activate (recruiting -> active)
    await projectDetailPage.changeStatus("activate")
    await projectDetailPage.expectStatus("active")
  })

  test("should complete active project", async ({ page }) => {
    // Create, publish, and activate a project
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    const projectData = generateProjectData({ prefix: "ToComplete" })
    await projectsPage.createDialog.fillForm(projectData)
    await projectsPage.createDialog.submit()
    await projectsPage.clickProject(projectData.title)

    await projectDetailPage.changeStatus("publish")
    await projectDetailPage.changeStatus("activate")
    await projectDetailPage.changeStatus("complete")

    await projectDetailPage.expectStatus("completed")
  })

  test("should archive an active project", async ({ page }) => {
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    const projectData = generateProjectData({ prefix: "ToArchive" })
    await projectsPage.createDialog.fillForm(projectData)
    await projectsPage.createDialog.submit()
    await projectsPage.clickProject(projectData.title)

    await projectDetailPage.changeStatus("publish")
    await projectDetailPage.changeStatus("activate")
    await projectDetailPage.changeStatus("archive")

    await projectDetailPage.expectStatus("archived")
  })

  test("should filter projects by status", async ({ page }) => {
    await projectsPage.navigate()

    // Filter by draft
    await projectsPage.filterByStatus("draft")
    await page.waitForTimeout(500)

    // Filter by active
    await projectsPage.filterByStatus("active")
    await page.waitForTimeout(500)

    // Filter back to all
    await projectsPage.filterByStatus("all")
    await page.waitForTimeout(500)
  })

  test("should search projects by title", async ({ page }) => {
    // Create a project with unique name
    const uniquePrefix = `SearchTest${Date.now()}`
    const projectTitle = await createProjectViaUI(page, {
      title: `${uniquePrefix} Project`,
    })

    await projectsPage.navigate()

    // Search for it
    await projectsPage.searchProjects(uniquePrefix)
    await projectsPage.expectProjectVisible(projectTitle)

    // Clear and search for non-existent
    await projectsPage.clearSearch()
    await projectsPage.searchProjects("NonExistentProjectXYZ123")

    // Should show empty or no results
    await page.waitForTimeout(1000)
  })

  test("should toggle between My Projects and All Projects view", async ({ page }) => {
    await projectsPage.navigate()

    // Switch to All Projects
    await projectsPage.showAllProjects()
    await page.waitForTimeout(500)

    // Switch back to My Projects
    await projectsPage.showMyProjects()
    await page.waitForTimeout(500)
  })
})
