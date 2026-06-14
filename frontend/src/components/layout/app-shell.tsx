'use client'

import { useState } from 'react'
import { Footer } from './footer'
import { Sidebar } from './sidebar'
import { Topbar } from './topbar'

export function AppShell({ children }: { children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false)

  return (
    <div className="min-h-screen bg-background">
      <Sidebar mobileOpen={mobileOpen} onMobileClose={() => setMobileOpen(false)} />

      {/* Main content area — offset by sidebar on desktop */}
      <div className="flex flex-col md:pl-[var(--sidebar-collapsed)]">
        <Topbar onMenuClick={() => setMobileOpen((o) => !o)} />
        <main className="min-h-screen px-4 py-6 md:px-6">{children}</main>
        <Footer />
      </div>
    </div>
  )
}
