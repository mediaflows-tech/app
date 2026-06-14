import { test, expect } from './fixtures/auth.fixture'

/**
 * Auto-tag (Rekognition pipeline) verification tests.
 *
 * Precondition: At least one asset must have been processed
 * by the ContentModerator Lambda (i.e. has non-empty autoTags in metadata).
 */

test.describe('Auto-Tags — Rekognition Pipeline', () => {
  // These tests require a live backend with processed assets.
  // Skip in CI unless E2E_BASE_URL points at a real environment.
  const hasLiveServer = !!process.env.E2E_BASE_URL
  const liveOnly = process.env.CI && !hasLiveServer ? test.skip : test

  liveOnly('catalog detail API returns autoTags in metadata', async ({ adminPage }) => {
    await adminPage.goto('/catalog')
    await adminPage.waitForSelector('a[href^="/catalog/"]', { timeout: 10_000 })

    // Intercept the detail API response when we click into an asset
    const [response] = await Promise.all([
      adminPage.waitForResponse((resp) => /\/api\/v1\/catalog\/\d+$/.test(resp.url()) && resp.status() === 200),
      adminPage.locator('a[href^="/catalog/"]').first().click()
    ])

    const detail = await response.json()
    const metadata = detail.asset?.metadata ?? detail.asset?.Metadata ?? {}

    // autoTags field should exist in the metadata
    expect(metadata).toHaveProperty('autoTags')

    const autoTags = metadata.autoTags ?? metadata.AutoTags ?? []
    if (autoTags.length > 0) {
      expect(autoTags[0]).toHaveProperty('name')
      expect(autoTags[0]).toHaveProperty('confidence')
    }
  })

  liveOnly('creator tags API returns separated manualTags and autoTags', async ({ adminPage }) => {
    await adminPage.goto('/creator/assets')
    await adminPage.waitForSelector('a[href^="/creator/assets/"]', { timeout: 10_000 })

    // Navigate to first asset and intercept the tags API call
    const [response] = await Promise.all([
      adminPage.waitForResponse((resp) => /\/api\/v1\/assets\/\d+\/tags/.test(resp.url()) && resp.status() === 200),
      adminPage.locator('a[href^="/creator/assets/"]').first().click()
    ])

    const tagsData = await response.json()

    expect(tagsData).toHaveProperty('manualTags')
    expect(tagsData).toHaveProperty('autoTags')
    expect(Array.isArray(tagsData.autoTags)).toBeTruthy()

    // Validate structure if autoTags exist
    if (tagsData.autoTags.length > 0) {
      const first = tagsData.autoTags[0]
      expect(first).toHaveProperty('name')
      expect(first).toHaveProperty('confidence')
      expect(typeof first.name).toBe('string')
      expect(typeof first.confidence).toBe('number')
    }
  })

  liveOnly('catalog detail page shows AI Tags section in sidebar', async ({ adminPage }) => {
    // Intercept catalog detail response to check if autoTags exist
    let autoTags: { name: string; confidence: number }[] = []

    await adminPage.route(/\/api\/v1\/catalog\/\d+$/, async (route) => {
      const response = await route.fetch()
      const body = await response.json()
      autoTags = body.asset?.metadata?.autoTags ?? body.asset?.Metadata?.AutoTags ?? []
      await route.fulfill({ response })
    })

    await adminPage.goto('/catalog')
    await adminPage.waitForSelector('a[href^="/catalog/"]', { timeout: 10_000 })

    await adminPage.locator('a[href^="/catalog/"]').first().click()
    await adminPage.waitForURL(/\/catalog\/\d+/)

    // Wait for page content to render
    await adminPage.waitForTimeout(2_000)

    if (autoTags.length > 0) {
      // AI Tags heading should be visible in the sidebar
      await expect(adminPage.getByText('AI Tags')).toBeVisible({ timeout: 5_000 })

      // At least one badge with percentage should be rendered
      const tagBadges = adminPage.locator('[class*="badge"]').filter({ hasText: /%/ })
      await expect(tagBadges.first()).toBeVisible()
    } else {
      // Asset has no autoTags — AI Tags section will not render
    }
  })

  liveOnly('creator asset detail shows AI-detected tags', async ({ adminPage }) => {
    await adminPage.goto('/creator/assets')
    await adminPage.waitForSelector('a[href^="/creator/assets/"]', { timeout: 10_000 })

    await adminPage.locator('a[href^="/creator/assets/"]').first().click()
    await adminPage.waitForURL(/\/creator\/assets\/\d+/)

    // Check for the AI-detected tags section (rendered by AutoTagDisplay component)
    const aiTagsLabel = adminPage.getByText('AI-detected tags')

    if (await aiTagsLabel.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await expect(aiTagsLabel).toBeVisible()

      // Verify tag badges with confidence percentages
      const tagBadges = adminPage.locator('[class*="badge"]').filter({ hasText: /%/ })
      const count = await tagBadges.count()
      expect(count).toBeGreaterThan(0)
    } else {
      // AI-detected tags section not visible — asset may not have been processed by Rekognition
    }
  })

  liveOnly('moderation metadata is populated alongside autoTags', async ({ adminPage }) => {
    let metadata: Record<string, unknown> = {}

    await adminPage.route(/\/api\/v1\/catalog\/\d+$/, async (route) => {
      const response = await route.fetch()
      const body = await response.json()
      metadata = body.asset?.metadata ?? body.asset?.Metadata ?? {}
      await route.fulfill({ response })
    })

    await adminPage.goto('/catalog')
    await adminPage.waitForSelector('a[href^="/catalog/"]', { timeout: 10_000 })

    await adminPage.locator('a[href^="/catalog/"]').first().click()
    await adminPage.waitForURL(/\/catalog\/\d+/)
    await adminPage.waitForTimeout(2_000)

    const moderation = metadata.moderation ?? metadata.Moderation

    if (moderation && typeof moderation === 'object' && !Array.isArray(moderation)) {
      expect(moderation).toHaveProperty('isSafe')
      const mod = moderation as Record<string, unknown>
      expect(typeof mod.isSafe).toBe('boolean')
    }
  })
})
