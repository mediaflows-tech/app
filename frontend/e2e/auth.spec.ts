import { test, expect } from '@playwright/test'
import { test as authTest } from './fixtures/auth.fixture'

test.describe('Authentication', () => {
  test('unauthenticated user is redirected to login', async ({ page }) => {
    await page.goto('/catalog')
    await page.waitForURL(/\/login/)
    await expect(page).toHaveURL(/\/login/)
  })

  test('unauthenticated user cannot access admin routes', async ({ page }) => {
    await page.goto('/admin')
    await page.waitForURL(/\/login/)
    await expect(page).toHaveURL(/\/login/)
  })

  test('unauthenticated user cannot access creator routes', async ({ page }) => {
    await page.goto('/creator/upload')
    await page.waitForURL(/\/login/)
    await expect(page).toHaveURL(/\/login/)
  })

  test('login page renders correctly', async ({ page }) => {
    await page.goto('/login')
    await expect(page.getByRole('heading', { name: /sign in/i })).toBeVisible()
    await expect(page.getByLabel(/email/i)).toBeVisible()
    await expect(page.getByLabel(/password/i)).toBeVisible()
  })
})

authTest.describe('Authenticated flows', () => {
  authTest('authenticated user can access dashboard', async ({ viewerPage }) => {
    await viewerPage.goto('/dashboard')
    await expect(viewerPage.locator('[data-testid="sidebar"]').or(viewerPage.getByRole('navigation'))).toBeVisible()
  })

  authTest('user can sign out', async ({ viewerPage }) => {
    await viewerPage.goto('/')

    // Open user menu and click sign out
    const userMenu = viewerPage
      .getByTestId('user-menu')
      .or(viewerPage.getByRole('button', { name: /avatar|user|account/i }))
    await userMenu.click()

    const signOutButton = viewerPage.getByRole('menuitem', { name: /sign out|log out/i })
    await signOutButton.click()

    // Should redirect to login
    await viewerPage.waitForURL(/\/login/, { timeout: 10_000 })
    await expect(viewerPage).toHaveURL(/\/login/)
  })

  authTest('viewer cannot access admin routes', async ({ viewerPage }) => {
    await viewerPage.goto('/admin')

    // Should either redirect or show forbidden
    const url = viewerPage.url()
    const isForbidden =
      url.includes('/login') ||
      (url.includes('/') && !url.includes('/admin')) ||
      (await viewerPage
        .getByText(/forbidden|unauthorized|access denied/i)
        .isVisible()
        .catch(() => false))

    expect(isForbidden).toBe(true)
  })

  authTest('a rejected access token (API 401) signs the user out', async ({ viewerPage }) => {
    // Simulate the backend rejecting the access token (expired/revoked). The api
    // client retries the 401 once, then forces a sign-out so the user isn't left
    // firing failing calls with a dead token.
    await viewerPage.route('**/api/v1/**', (route) =>
      route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Unauthorized' })
      })
    )

    await viewerPage.goto('/catalog')

    await viewerPage.waitForURL(/\/login/, { timeout: 15_000 })
    await expect(viewerPage).toHaveURL(/error=SessionRequired/)
  })
})
