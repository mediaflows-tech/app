import { test, expect } from './fixtures/auth.fixture'
import path from 'path'

test.describe('Upload', () => {
  // Note: upload tests that interact with S3 are skipped in CI.
  // They require a running API with valid AWS credentials.
  const skipInCI = process.env.CI ? test.skip : test

  test('upload page loads for creator', async ({ creatorPage }) => {
    await creatorPage.goto('/creator/upload')
    await expect(creatorPage.getByRole('heading', { name: /upload/i })).toBeVisible()

    // Dropzone should be visible
    await expect(creatorPage.getByText(/drag.*drop|click.*upload|browse/i)).toBeVisible()
  })

  test('upload page is not accessible to viewer', async ({ viewerPage }) => {
    await viewerPage.goto('/creator/upload')

    // Should redirect away from upload page
    const url = viewerPage.url()
    expect(url).not.toContain('/creator/upload')
  })

  skipInCI('upload a file and verify it appears in asset library', async ({ creatorPage }) => {
    await creatorPage.goto('/creator/upload')

    // Create a test file to upload
    const fileInput = creatorPage.locator('input[type="file"]')

    // Upload a small test image
    await fileInput.setInputFiles({
      name: 'test-image.png',
      mimeType: 'image/png',
      buffer: Buffer.alloc(100, 0) // Minimal PNG-like buffer for test
    })

    // Wait for upload progress and completion
    await expect(creatorPage.getByText(/uploading|processing|complete|uploaded/i)).toBeVisible({ timeout: 30_000 })

    // Navigate to asset library and verify the file appears
    await creatorPage.goto('/creator/assets')
    await creatorPage.waitForTimeout(2000) // Allow indexing

    await expect(creatorPage.getByText('test-image')).toBeVisible({ timeout: 10_000 })
  })

  test('creator can view asset library', async ({ creatorPage }) => {
    await creatorPage.goto('/creator/assets')
    await expect(creatorPage.getByRole('heading', { name: /assets|library|my.*assets/i })).toBeVisible()
  })
})
