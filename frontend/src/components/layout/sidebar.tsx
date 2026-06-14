'use client'

import { useState, useEffect, useCallback } from 'react'
import Link from 'next/link'
import Image from 'next/image'
import { usePathname } from 'next/navigation'
import { useSession } from 'next-auth/react'
import { useTheme } from 'next-themes'
import {
  LayoutDashboard,
  Upload,
  Images,
  Globe,
  Search,
  Bookmark,
  ClipboardCheck,
  CalendarDays,
  Users,
  ScrollText,
  Activity,
  Menu
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { Badge } from '@/components/ui/badge'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import type { UserRole } from '@/types/auth'
import { useBadges } from '@/hooks/use-badges'

interface NavItem {
  label: string
  href: string
  icon: React.ElementType
  badge?: string
  /** Key in the live badge store — overrides `badge` when a value is present. */
  badgeKey?: string
  roles?: UserRole[]
}

interface NavSection {
  title: string
  items: NavItem[]
}

const NAV_SECTIONS: NavSection[] = [
  {
    title: 'Overview',
    items: [{ label: 'Dashboard', href: '/dashboard', icon: LayoutDashboard }]
  },
  {
    title: 'Admin',
    items: [
      { label: 'Summary', href: '/admin', icon: Activity, roles: ['SystemAdmin'] },
      { label: 'Users', href: '/admin/users', icon: Users, roles: ['SystemAdmin'] },
      { label: 'Audit Logs', href: '/admin/audit-logs', icon: ScrollText, roles: ['SystemAdmin'] },
      { label: 'Monitoring', href: '/admin/monitoring', icon: Activity, roles: ['SystemAdmin'] }
    ]
  },
  {
    title: 'Create',
    items: [
      { label: 'Upload', href: '/creator/upload', icon: Upload, roles: ['SystemAdmin', 'ContentCreator'] },
      { label: 'Assets', href: '/creator/assets', icon: Images, roles: ['SystemAdmin', 'ContentCreator'] }
    ]
  },
  {
    title: 'Review',
    items: [
      {
        label: 'Review Queue',
        href: '/review',
        icon: ClipboardCheck,
        roles: ['SystemAdmin', 'Editor'],
        badgeKey: 'pending-review-count'
      },
      { label: 'Schedule', href: '/schedule', icon: CalendarDays, roles: ['SystemAdmin', 'Editor'] }
    ]
  },
  {
    title: 'Browse',
    items: [
      { label: 'Catalog', href: '/catalog', icon: Globe },
      { label: 'Search', href: '/search', icon: Search },
      { label: 'Bookmarks', href: '/bookmarks', icon: Bookmark }
    ]
  }
]

function canAccess(item: NavItem, role: UserRole): boolean {
  if (!item.roles) return true
  return item.roles.includes(role)
}

interface SidebarProps {
  mobileOpen: boolean
  onMobileClose: () => void
}

export function Sidebar({ mobileOpen, onMobileClose }: SidebarProps) {
  const [collapsed, setCollapsed] = useState(true)
  const pathname = usePathname()
  const { data: session } = useSession()
  const role = session?.user.role ?? 'Viewer'
  const badges = useBadges()

  // Close mobile sidebar on route change
  useEffect(() => {
    onMobileClose()
  }, [pathname, onMobileClose])

  const handleMouseEnter = useCallback(() => setCollapsed(false), [])
  const handleMouseLeave = useCallback(() => setCollapsed(true), [])

  const { resolvedTheme } = useTheme()
  const logoSrc = resolvedTheme === 'dark' ? '/mediaflows-dark.png' : '/mediaflows-light.png'

  const renderContent = (isCollapsed: boolean) => (
    <TooltipProvider delay={0}>
      <div className="flex h-full flex-col">
        {/* Logo / Brand — icon container keeps logo aligned with nav icons */}
        <Link href="/" className="flex h-[var(--topbar-height)] shrink-0 items-center gap-2.5 px-4">
          <span className="relative flex h-6 w-8 shrink-0 items-center justify-center">
            <Image
              key="light"
              src="/mediaflows-light.png"
              alt="MediaFlows"
              width={20}
              height={20}
              className={cn(
                'absolute rounded-sm transition-opacity duration-300',
                resolvedTheme === 'dark' ? 'opacity-0' : 'opacity-100'
              )}
            />
            <Image
              key="dark"
              src="/mediaflows-dark.png"
              alt="MediaFlows"
              width={20}
              height={20}
              className={cn(
                'absolute rounded-sm transition-opacity duration-300',
                resolvedTheme === 'dark' ? 'opacity-100' : 'opacity-0'
              )}
            />
          </span>
          <span
            className={cn(
              'whitespace-nowrap text-sm font-semibold tracking-tight text-sidebar-foreground transition-opacity duration-200',
              isCollapsed ? 'opacity-0' : 'opacity-100'
            )}
          >
            MediaFlows
          </span>
        </Link>

        {/* Nav */}
        <nav className="flex-1 overflow-x-hidden overflow-y-auto px-2 py-3">
          {NAV_SECTIONS.map((section, sectionIdx) => {
            const visibleItems = section.items.filter((item) => canAccess(item, role as UserRole))
            if (visibleItems.length === 0) return null

            return (
              <div key={section.title} className={cn(sectionIdx > 0 && 'mt-3')}>
                {/* Section heading — crossfades between divider (collapsed) and text (expanded) */}
                <div className="relative mb-1 flex h-5 items-center">
                  <Separator
                    className={cn(
                      'absolute inset-x-0 bg-sidebar-border transition-opacity duration-200',
                      isCollapsed ? 'opacity-100' : 'opacity-0'
                    )}
                  />
                  <p
                    className={cn(
                      'whitespace-nowrap px-2 text-[10px] font-semibold uppercase tracking-widest text-sidebar-foreground/40 transition-opacity duration-200',
                      isCollapsed ? 'opacity-0' : 'opacity-100'
                    )}
                  >
                    {section.title}
                  </p>
                </div>

                <ul className="space-y-0.5">
                  {visibleItems.map((item) => {
                    const isActive =
                      pathname === item.href || (item.href !== '/admin' && pathname.startsWith(item.href + '/'))
                    const Icon = item.icon

                    const liveBadge = item.badgeKey ? badges[item.badgeKey] : undefined
                    const badgeValue = liveBadge ?? item.badge
                    const showBadge = !!badgeValue && badgeValue !== '0'

                    const linkContent = (
                      <Link
                        href={item.href}
                        className={cn(
                          'group flex items-center gap-2.5 rounded-md px-2 py-1.5 text-sm transition-colors',
                          'text-sidebar-foreground/70 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
                          isActive && 'bg-sidebar-accent text-sidebar-accent-foreground font-medium'
                        )}
                      >
                        <span className="flex w-8 shrink-0 items-center justify-center">
                          <Icon
                            className={cn(
                              'h-4 w-4 shrink-0',
                              isActive
                                ? 'text-sidebar-accent-foreground'
                                : 'text-sidebar-foreground/50 group-hover:text-sidebar-accent-foreground'
                            )}
                          />
                        </span>
                        <span className="flex-1 truncate whitespace-nowrap">{item.label}</span>
                        {showBadge && (
                          <Badge variant="secondary" className="h-4 shrink-0 px-1 text-[10px]">
                            {badgeValue}
                          </Badge>
                        )}
                      </Link>
                    )

                    if (isCollapsed) {
                      return (
                        <li key={item.href}>
                          <Tooltip>
                            <TooltipTrigger render={linkContent} />
                            <TooltipContent side="right" className="text-xs">
                              {item.label}
                            </TooltipContent>
                          </Tooltip>
                        </li>
                      )
                    }

                    return <li key={item.href}>{linkContent}</li>
                  })}
                </ul>
              </div>
            )
          })}
        </nav>
      </div>
    </TooltipProvider>
  )

  return (
    <>
      {/* Desktop sidebar */}
      <aside
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
        style={
          {
            '--sidebar-current-width': collapsed ? 'var(--sidebar-collapsed)' : 'var(--sidebar-width)'
          } as React.CSSProperties
        }
        className={cn(
          'fixed inset-y-0 left-0 z-30 hidden flex-col overflow-hidden',
          'w-[var(--sidebar-current-width)] transition-[width] duration-200 ease-in-out',
          'sidebar-glass',
          'md:flex'
        )}
      >
        {renderContent(collapsed)}
      </aside>

      {/* Desktop backdrop blur when expanded */}
      <div
        className={cn(
          'sidebar-expanded-backdrop fixed inset-0 z-[25] hidden md:block',
          collapsed ? 'pointer-events-none opacity-0' : 'opacity-100'
        )}
        onClick={() => setCollapsed(true)}
        aria-hidden="true"
      />

      {/* Mobile backdrop */}
      {mobileOpen && (
        <div className="fixed inset-0 z-40 bg-black/50 md:hidden" onClick={onMobileClose} aria-hidden="true" />
      )}

      {/* Mobile slide-in panel */}
      <aside
        className={cn(
          'fixed inset-y-0 left-0 z-50 flex flex-col',
          'w-[var(--sidebar-width)] bg-background',
          'transition-transform duration-200 ease-in-out md:hidden',
          mobileOpen ? 'translate-x-0' : '-translate-x-full'
        )}
      >
        {renderContent(false)}
      </aside>
    </>
  )
}

// Mobile menu trigger — used in topbar
export function MobileMenuButton({ onClick }: { onClick: () => void }) {
  return (
    <Button variant="ghost" size="icon" className="h-8 w-8 md:hidden" onClick={onClick} aria-label="Open menu">
      <Menu className="h-4 w-4" />
    </Button>
  )
}
