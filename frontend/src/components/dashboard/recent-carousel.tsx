'use client'

import { useState, useCallback, useEffect } from 'react'
import Link from 'next/link'
import { useSession } from 'next-auth/react'
import { Upload, Images, ClipboardCheck, Globe, Search, Bookmark, CalendarDays, Package, Sparkles } from 'lucide-react'
import useEmblaCarousel from 'embla-carousel-react'
import Autoplay from 'embla-carousel-autoplay'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import { cn } from '@/lib/utils'
import type { PagedResult, MediaAssetSummaryDto } from '@/types/api'
import type { UserRole } from '@/types/auth'

/* ── Role-gated quick actions ── */

interface QuickAction {
  label: string
  href: string
  icon: React.ElementType
  description: string
  roles?: UserRole[]
}

const QUICK_ACTIONS: QuickAction[] = [
  {
    label: 'Upload',
    href: '/creator/upload',
    icon: Upload,
    description: 'Add new media to your library.',
    roles: ['SystemAdmin', 'ContentCreator']
  },
  {
    label: 'Assets',
    href: '/creator/assets',
    icon: Images,
    description: 'Manage your uploaded content.',
    roles: ['SystemAdmin', 'ContentCreator']
  },
  {
    label: 'Review Queue',
    href: '/review',
    icon: ClipboardCheck,
    description: 'Review and approve pending submissions.',
    roles: ['SystemAdmin', 'Editor']
  },
  {
    label: 'Schedule',
    href: '/schedule',
    icon: CalendarDays,
    description: 'Plan content publishing dates.',
    roles: ['SystemAdmin', 'Editor']
  },
  {
    label: 'Catalog',
    href: '/catalog',
    icon: Globe,
    description: 'Browse the full media library.'
  },
  {
    label: 'Search',
    href: '/search',
    icon: Search,
    description: 'Find any asset across the platform.'
  },
  {
    label: 'Bookmarks',
    href: '/bookmarks',
    icon: Bookmark,
    description: 'Your saved assets for quick access.'
  }
]

function canAccess(action: QuickAction, role: UserRole): boolean {
  if (!action.roles) return true
  return action.roles.includes(role)
}

/* ── Carousel content ── */

interface CarouselContentProps {
  assets: MediaAssetSummaryDto[]
  role: UserRole
}

// Shared glass surface — matches topbar's bg-background/80 backdrop-blur-md
const GLASS_SURFACE = 'bg-background/80 backdrop-blur-md border border-border/50 text-foreground'

function CarouselContent({ assets, role }: CarouselContentProps) {
  const [emblaRef, emblaApi] = useEmblaCarousel({ loop: true }, [Autoplay({ delay: 5000, stopOnInteraction: false })])
  const [selectedIndex, setSelectedIndex] = useState(0)
  const [hoveredAction, setHoveredAction] = useState<QuickAction | null>(null)
  const visibleActions = QUICK_ACTIONS.filter((a) => canAccess(a, role))

  const onSelect = useCallback(() => {
    if (!emblaApi) return
    setSelectedIndex(emblaApi.selectedScrollSnap())
  }, [emblaApi])

  useEffect(() => {
    if (!emblaApi) return
    onSelect()
    emblaApi.on('select', onSelect)
    return () => {
      emblaApi.off('select', onSelect)
    }
  }, [emblaApi, onSelect])

  const currentAsset = assets[selectedIndex]
  const hasAssets = assets.length > 0

  return (
    <div className="relative h-full w-full overflow-hidden rounded-2xl border border-border/50 bg-muted">
      {/* Embla viewport */}
      <div className="h-full" ref={emblaRef}>
        <div className="flex h-full">
          {!hasAssets ? (
            <div className="flex h-full w-full min-w-0 flex-[0_0_100%] items-center justify-center">
              <p className="text-sm text-muted-foreground">No assets yet</p>
            </div>
          ) : (
            assets.map((asset) => {
              const src = asset.previewUrl || asset.thumbnailUrl
              return (
                <div key={asset.id} className="relative h-full min-w-0 flex-[0_0_100%]">
                  {src ? (
                    <img src={src} alt={asset.title} className="h-full w-full object-cover" />
                  ) : (
                    <div className="flex h-full items-center justify-center text-muted-foreground">
                      <Package className="h-16 w-16" />
                    </div>
                  )}
                </div>
              )
            })
          )}
        </div>
      </div>

      {/* Top-left: role-based quick action buttons */}
      <div className="absolute left-4 top-4 z-10 flex gap-2">
        {visibleActions.map((action) => {
          const Icon = action.icon
          const isHovered = hoveredAction?.href === action.href
          return (
            <Link
              key={action.href}
              href={action.href}
              prefetch={false}
              onMouseEnter={() => setHoveredAction(action)}
              onMouseLeave={() => setHoveredAction(null)}
              aria-label={action.label}
              className={cn(
                'flex h-11 shrink-0 items-center justify-start overflow-hidden rounded-xl px-3.5 transition-[max-width,padding] duration-300 ease-in-out',
                GLASS_SURFACE
              )}
            >
              <Icon className="h-4 w-4 shrink-0" />
              <span
                className={cn(
                  'overflow-hidden whitespace-nowrap text-sm font-medium transition-[max-width,margin,opacity] duration-300 ease-in-out',
                  isHovered ? 'ml-2 max-w-[200px] opacity-100' : 'ml-0 max-w-0 opacity-0'
                )}
              >
                {action.label}
              </span>
            </Link>
          )
        })}
      </div>

      {/* Bottom-left: current asset title card */}
      {currentAsset && (
        <Link
          href={`/catalog/${currentAsset.id}`}
          prefetch={false}
          className={cn(
            'absolute bottom-6 left-6 z-10 max-w-md rounded-xl px-5 py-4 transition-all duration-300 ease-in-out',
            GLASS_SURFACE,
            'hover:scale-[1.02]'
          )}
        >
          <p className="text-base font-semibold">{currentAsset.title}</p>
          <p className="text-xs text-muted-foreground">By {currentAsset.creatorName}</p>
        </Link>
      )}

      {/* Bottom-right: dynamic description card — reacts to hovered button (defaults to first action) */}
      <div
        className={cn(
          'absolute bottom-6 right-6 z-10 w-64 rounded-xl px-5 py-4 transition-all duration-300 ease-in-out',
          GLASS_SURFACE
        )}
      >
        <div className="flex items-center gap-2">
          <Sparkles className="h-3.5 w-3.5 transition-all duration-300 ease-in-out" />
          <p className="text-xs font-semibold uppercase tracking-widest transition-all duration-300 ease-in-out">
            {(hoveredAction ?? visibleActions[0])?.label ?? 'Quick Actions'}
          </p>
        </div>
        <p className="mt-2 text-xs leading-relaxed text-muted-foreground transition-all duration-300 ease-in-out">
          {(hoveredAction ?? visibleActions[0])?.description ?? 'Hover an icon to explore.'}
        </p>
      </div>

      {/* Bottom-center: dot pagination */}
      {assets.length > 1 && (
        <div className="absolute bottom-2 left-1/2 z-10 flex -translate-x-1/2 gap-1.5">
          {assets.map((_, i) => (
            <span
              key={i}
              role="button"
              tabIndex={0}
              onClick={() => emblaApi?.scrollTo(i)}
              className={cn(
                'h-1.5 cursor-pointer rounded-full bg-foreground/40 transition-all duration-300 ease-in-out',
                i === selectedIndex ? 'w-4 bg-foreground' : 'w-1.5'
              )}
            />
          ))}
        </div>
      )}
    </div>
  )
}

/* ── Main: just the carousel (no tabs) ── */

export function RecentCarousel() {
  const { data: session } = useSession()
  const role = (session?.user.role ?? 'Viewer') as UserRole

  const { data, isLoading } = useQuery({
    queryKey: ['dashboard', 'recent-assets'],
    queryFn: () => api.get<PagedResult<MediaAssetSummaryDto>>('/catalog?page=1'),
    staleTime: 60_000
  })

  const assets = data?.items?.slice(0, 6) ?? []

  if (isLoading) {
    return <div className="h-full w-full animate-pulse rounded-2xl bg-muted" />
  }

  return <CarouselContent assets={assets} role={role} />
}
