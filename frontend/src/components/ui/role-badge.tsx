'use client'

import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

const ROLE_COLORS: Record<string, string> = {
  SystemAdmin: 'bg-violet-500/10 text-violet-600 dark:text-violet-400',
  ContentCreator: 'bg-blue-500/10 text-blue-600 dark:text-blue-400',
  Editor: 'bg-amber-500/10 text-amber-600 dark:text-amber-400',
  Viewer: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
}

function formatRole(role: string): string {
  return role.replace(/([a-z])([A-Z])/g, '$1 $2')
}

interface RoleBadgeProps {
  role: string
  className?: string
}

export function RoleBadge({ role, className }: RoleBadgeProps) {
  return (
    <Badge variant="secondary" className={cn('text-xs', ROLE_COLORS[role], className)}>
      {formatRole(role)}
    </Badge>
  )
}
