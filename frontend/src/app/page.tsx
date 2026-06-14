import type { Metadata } from 'next'
import { PrismHero } from '@/components/landing/prism-hero'

export const metadata: Metadata = {
  title: 'MediaFlows',
  description: 'The workspace where teams gather, review and ship media.'
}

export default function LandingPage() {
  return (
    <>
      <link rel="preload" as="image" href="/landing/hero-poster.webp" fetchPriority="high" />
      <PrismHero />
    </>
  )
}
