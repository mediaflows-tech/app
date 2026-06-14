import { test as base, expect, type Page } from '@playwright/test'

// Storage state paths for each role
const STORAGE_DIR = 'e2e/.auth'

type AuthFixtures = {
  adminPage: Page
  creatorPage: Page
  editorPage: Page
  viewerPage: Page
}

/**
 * Authenticate via the Cognito-backed login flow.
 * Uses NEXTAUTH_URL callback — the test user credentials come from env vars.
 */
async function loginAs(page: Page, role: 'admin' | 'creator' | 'editor' | 'viewer') {
  const credentials: Record<string, { email: string; password: string }> = {
    admin: {
      email: process.env.E2E_ADMIN_EMAIL || 'admin@example.com',
      password: process.env.E2E_ADMIN_PASSWORD || 'Test1234!'
    },
    creator: {
      email: process.env.E2E_CREATOR_EMAIL || 'creator@example.com',
      password: process.env.E2E_CREATOR_PASSWORD || 'Test1234!'
    },
    editor: {
      email: process.env.E2E_EDITOR_EMAIL || 'editor@example.com',
      password: process.env.E2E_EDITOR_PASSWORD || 'Test1234!'
    },
    viewer: {
      email: process.env.E2E_VIEWER_EMAIL || 'viewer@example.com',
      password: process.env.E2E_VIEWER_PASSWORD || 'Test1234!'
    }
  }

  const { email, password } = credentials[role]

  // Navigate to login page — NextAuth redirects to Cognito hosted UI
  await page.goto('/login')

  // Wait for Cognito hosted UI or custom login form
  // If using custom login page (auth-client.ts), fill the form directly
  await page.waitForURL(/\/(login|authorize)/)

  // Fill credentials on the login form
  const emailInput = page.getByLabel(/email/i).or(page.locator('#signInFormUsername'))
  const passwordInput = page.getByLabel(/password/i).or(page.locator('#signInFormPassword'))

  await emailInput.fill(email)
  await passwordInput.fill(password)

  // Submit
  const submitButton = page.getByRole('button', { name: /sign in/i }).or(page.locator('input[type="submit"]'))
  await submitButton.click()

  // Wait for redirect back to the app (lands on /dashboard or other app page)
  await page.waitForURL((url) => !url.pathname.includes('/login') && !url.pathname.includes('/authorize'), {
    timeout: 30_000
  })
  await expect(page.locator('body')).not.toContainText('Sign in')
}

export const test = base.extend<AuthFixtures>({
  adminPage: async ({ browser }, use) => {
    const context = await browser.newContext()
    const page = await context.newPage()
    await loginAs(page, 'admin')
    await use(page)
    await context.close()
  },

  creatorPage: async ({ browser }, use) => {
    const context = await browser.newContext()
    const page = await context.newPage()
    await loginAs(page, 'creator')
    await use(page)
    await context.close()
  },

  editorPage: async ({ browser }, use) => {
    const context = await browser.newContext()
    const page = await context.newPage()
    await loginAs(page, 'editor')
    await use(page)
    await context.close()
  },

  viewerPage: async ({ browser }, use) => {
    const context = await browser.newContext()
    const page = await context.newPage()
    await loginAs(page, 'viewer')
    await use(page)
    await context.close()
  }
})

export { expect } from '@playwright/test'
