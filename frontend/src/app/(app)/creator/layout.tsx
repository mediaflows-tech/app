import type { ReactNode } from 'react'

interface CreatorLayoutProps {
  children: ReactNode
}

export default function CreatorLayout({ children }: CreatorLayoutProps) {
  return <>{children}</>
}
