'use client'

import Masonry from 'react-masonry-css'
import { cn } from '@/lib/utils'

interface MasonryGridProps {
  children: React.ReactNode
  className?: string
  columnClassName?: string
}

const DEFAULT_BREAKPOINTS = {
  default: 4,
  1280: 3,
  1024: 3,
  768: 2,
  640: 1
}

export function MasonryGrid({ children, className, columnClassName }: MasonryGridProps) {
  return (
    <Masonry
      breakpointCols={DEFAULT_BREAKPOINTS}
      className={cn('flex w-full -ml-4', className)}
      columnClassName={cn('pl-4 bg-clip-padding', columnClassName)}
    >
      {children}
    </Masonry>
  )
}
