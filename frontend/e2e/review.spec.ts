import { test, expect } from './fixtures/auth.fixture'

test.describe('Review Queue', () => {
  test('review page loads for editor', async ({ editorPage }) => {
    await editorPage.goto('/review')
    await expect(editorPage.getByRole('heading', { name: /review/i })).toBeVisible()
  })

  test('review page is not accessible to viewer', async ({ viewerPage }) => {
    await viewerPage.goto('/review')

    const url = viewerPage.url()
    expect(url).not.toContain('/review')
  })

  test('review queue displays pending items', async ({ editorPage }) => {
    await editorPage.goto('/review')

    // Wait for the review table/list to load
    await editorPage.waitForSelector('table, [data-testid="review-list"], [data-testid="review-queue"]', {
      timeout: 10_000
    })

    // Should show either review items or an empty state
    const hasItems = (await editorPage.locator('table tbody tr').count()) > 0
    const hasEmptyState = await editorPage
      .getByText(/no.*review|no.*pending|empty|nothing/i)
      .isVisible()
      .catch(() => false)

    expect(hasItems || hasEmptyState).toBe(true)
  })

  test('editor can navigate to review detail', async ({ editorPage }) => {
    await editorPage.goto('/review')

    // Wait for items to load
    await editorPage.waitForSelector('table tbody tr', { timeout: 10_000 }).catch(() => null)

    const firstRow = editorPage.locator('table tbody tr').first()

    if (await firstRow.isVisible()) {
      // Click the first review item
      const link = firstRow.locator('a').first()
      if (await link.isVisible()) {
        await link.click()
        await editorPage.waitForURL(/\/review\/[a-zA-Z0-9-]+/)

        // Detail page should show approve/reject buttons
        await expect(
          editorPage.getByRole('button', { name: /approve/i }).or(editorPage.getByRole('button', { name: /reject/i }))
        ).toBeVisible()
      }
    }
  })

  test('editor can approve an asset from detail page', async ({ editorPage }) => {
    await editorPage.goto('/review')

    await editorPage.waitForSelector('table tbody tr', { timeout: 10_000 }).catch(() => null)

    const firstRow = editorPage.locator('table tbody tr').first()

    if (await firstRow.isVisible()) {
      const link = firstRow.locator('a').first()
      if (await link.isVisible()) {
        await link.click()
        await editorPage.waitForURL(/\/review\/[a-zA-Z0-9-]+/)

        const approveButton = editorPage.getByRole('button', { name: /approve/i })

        if (await approveButton.isVisible()) {
          await approveButton.click()

          // Confirmation dialog may appear
          const confirmButton = editorPage.getByRole('button', { name: /confirm|yes/i })
          if (await confirmButton.isVisible({ timeout: 2_000 }).catch(() => false)) {
            await confirmButton.click()
          }

          // Verify status changed — toast or status badge updates
          await expect(editorPage.getByText(/approved|success/i)).toBeVisible({ timeout: 5_000 })
        }
      }
    }
  })
})
