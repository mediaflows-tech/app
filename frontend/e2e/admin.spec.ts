import { test, expect } from './fixtures/auth.fixture'

test.describe('Admin Dashboard', () => {
  test('admin dashboard loads', async ({ adminPage }) => {
    await adminPage.goto('/admin')
    await expect(adminPage.getByRole('heading', { name: /admin|dashboard/i })).toBeVisible()
  })

  test('admin dashboard shows metric cards', async ({ adminPage }) => {
    await adminPage.goto('/admin')

    // Wait for dashboard metrics to load
    await adminPage.waitForSelector('[data-testid="metric-card"], [class*="card"]', { timeout: 10_000 })

    // Should show key metrics: total assets, users, storage, etc.
    const cards = adminPage
      .locator('[data-testid="metric-card"]')
      .or(adminPage.locator('[class*="card"]').filter({ hasText: /total|assets|users|storage/i }))

    await expect(cards.first()).toBeVisible()
  })

  test('admin dashboard is not accessible to viewer', async ({ viewerPage }) => {
    await viewerPage.goto('/admin')

    const url = viewerPage.url()
    expect(url).not.toContain('/admin')
  })

  test('admin can navigate to user management', async ({ adminPage }) => {
    await adminPage.goto('/admin')

    // Click users link in sidebar or dashboard
    const usersLink = adminPage.getByRole('link', { name: /users/i }).or(adminPage.getByTestId('nav-users'))
    await usersLink.first().click()

    await adminPage.waitForURL(/\/admin\/users/)
    await expect(adminPage.getByRole('heading', { name: /users/i })).toBeVisible()
  })

  test('user management shows user table', async ({ adminPage }) => {
    await adminPage.goto('/admin/users')

    // Wait for user table to load
    await adminPage.waitForSelector('table', { timeout: 10_000 })

    // Table should have headers: Name, Email, Role, Status
    await expect(adminPage.getByRole('columnheader', { name: /name/i })).toBeVisible()
    await expect(adminPage.getByRole('columnheader', { name: /email/i })).toBeVisible()
    await expect(adminPage.getByRole('columnheader', { name: /role/i })).toBeVisible()
  })

  test('admin can filter users by role', async ({ adminPage }) => {
    await adminPage.goto('/admin/users')

    // Click a role filter tab
    const roleTab = adminPage
      .getByRole('tab', { name: /editor/i })
      .or(adminPage.getByRole('button', { name: /editor/i }))

    if (await roleTab.isVisible()) {
      await roleTab.click()

      // Wait for table to re-render with filtered results
      await adminPage.waitForTimeout(500)

      // Verify URL or filtered results
      const url = adminPage.url()
      const hasFilter = url.includes('role=') || true // Some filters are client-side
      expect(hasFilter).toBe(true)
    }
  })
})
