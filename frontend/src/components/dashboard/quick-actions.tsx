'use client'

import Link from 'next/link'
import { Upload, Images, ClipboardCheck, Globe } from 'lucide-react'

const ACTIONS = [
  { label: 'Upload', href: '/creator/upload', icon: Upload },
  { label: 'Assets', href: '/creator/assets', icon: Images },
  { label: 'Review Queue', href: '/review', icon: ClipboardCheck },
  { label: 'Catalog', href: '/catalog', icon: Globe }
]

export function QuickActions() {
  return (
    <div className="grid flex-1 grid-cols-2 gap-4">
      {ACTIONS.map((action) => {
        const Icon = action.icon
        return (
          <Link
            key={action.href}
            href={action.href}
            className="group flex flex-col items-center justify-center gap-3 rounded-xl border border-border bg-muted/50 text-center transition-colors hover:bg-accent/50 dark:bg-card"
          >
            <Icon className="h-7 w-7 text-muted-foreground transition-colors group-hover:text-foreground" />
            <span className="text-sm font-medium">{action.label}</span>
          </Link>
        )
      })}
    </div>
  )
}
