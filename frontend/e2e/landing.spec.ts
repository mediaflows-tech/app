import { test, expect } from '@playwright/test'

test.describe('Landing page (PrismHero)', () => {
  test('renders the hero structure', async ({ page }) => {
    const errors: string[] = []
    page.on('pageerror', (err) => errors.push(err.message))
    page.on('console', (msg) => {
      if (msg.type() === 'error') errors.push(msg.text())
    })

    await page.goto('/')

    // Wordmark — h1 contains "MediaFlows" (no asterisk)
    const wordmark = page.getByTestId('hero-wordmark')
    await expect(wordmark).toBeVisible()
    await expect(wordmark).toContainText('MediaFlows')

    // The top-pill nav and its Sign in link have been removed
    await expect(page.getByTestId('hero-nav-signin')).toHaveCount(0)

    // Primary CTA — Get started link to /login
    const ctaLink = page.getByTestId('hero-cta-getstarted')
    await expect(ctaLink).toBeVisible()
    await expect(ctaLink).toHaveAttribute('href', '/login')

    // Background video element is rendered (motion-enabled environment)
    const video = page.getByTestId('hero-video')
    await expect(video).toBeAttached()
    await expect(video).toHaveAttribute('playsinline', '')
    await expect(video).toHaveAttribute('muted', '')
    await expect(video).toHaveAttribute('loop', '')

    expect(errors, 'no console/page errors on /').toEqual([])
  })

  test('Get started link navigates to /login', async ({ page }) => {
    await page.goto('/')
    await page.getByTestId('hero-cta-getstarted').click()
    await page.waitForURL(/\/login/)
    await expect(page).toHaveURL(/\/login/)
  })

  test('reduced-motion users see the poster image instead of the video', async ({ browser }) => {
    const context = await browser.newContext({ reducedMotion: 'reduce' })
    const page = await context.newPage()
    try {
      await page.goto('/')
      await expect(page.getByTestId('hero-poster')).toBeVisible()
      await expect(page.getByTestId('hero-video')).toHaveCount(0)
    } finally {
      await context.close()
    }
  })
})
