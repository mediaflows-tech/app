'use client'

import { useTheme } from 'next-themes'
import { Sun, Moon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { NotificationPanel } from './notification-panel'
import { UserMenu } from './user-menu'
import { MobileMenuButton } from './sidebar'

interface TopbarProps {
  onMenuClick: () => void
}

export function Topbar({ onMenuClick }: TopbarProps) {
  const { theme, setTheme } = useTheme()

  return (
    <header className="sticky top-0 z-20 flex h-[var(--topbar-height)] items-center bg-background/80 backdrop-blur-md px-4 gap-2">
      {/* Mobile menu button */}
      <MobileMenuButton onClick={onMenuClick} />

      {/* Spacer */}
      <div className="flex-1" />

      {/* Actions */}
      <div className="flex items-center gap-1">
        {/* Theme toggle */}
        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8"
          onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
          aria-label="Toggle theme"
        >
          <Sun className="h-4 w-4 rotate-0 scale-100 transition-all dark:-rotate-90 dark:scale-0" />
          <Moon className="absolute h-4 w-4 rotate-90 scale-0 transition-all dark:rotate-0 dark:scale-100" />
        </Button>

        {/* Notifications */}
        <NotificationPanel />

        {/* User menu */}
        <UserMenu />
      </div>
    </header>
  )
}
