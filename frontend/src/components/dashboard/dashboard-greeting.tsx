'use client'

import { useState, useEffect } from 'react'
import { PageHeader } from '@/components/shared/page-header'

function getGreeting(): string {
  const hour = new Date().getHours()
  if (hour < 12) return 'Good morning'
  if (hour < 18) return 'Good afternoon'
  return 'Good evening'
}

export function DashboardGreeting({ name }: { name: string }) {
  const [greeting, setGreeting] = useState('Welcome back')

  // Defer time-based greeting to client to avoid hydration mismatch
  useEffect(() => {
    setGreeting(getGreeting())
  }, [])

  return <PageHeader title={`${greeting}, ${name}`} description="Here's your workspace at a glance." />
}
