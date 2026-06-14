'use client'

import { useCallback, useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { signOut, useSession } from 'next-auth/react'
import { useRouter } from 'next/navigation'
import { LogOut, User } from 'lucide-react'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar'
import { cn } from '@/lib/utils'

function getInitials(name?: string | null, email?: string | null): string {
  if (name) {
    const parts = name.trim().split(/\s+/)
    if (parts.length >= 2) return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
    return name.slice(0, 2).toUpperCase()
  }
  if (email) return email.slice(0, 2).toUpperCase()
  return 'U'
}

export function UserMenu() {
  const { data: session } = useSession()
  const router = useRouter()
  const [visible, setVisible] = useState(false)
  const [mounted, setMounted] = useState(false)
  const [pos, setPos] = useState({ top: 0, right: 0 })
  const timerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const popoverRef = useRef<HTMLDivElement>(null)
  const user = session?.user
  const initials = getInitials(user?.name, user?.email)

  const open = () => {
    if (triggerRef.current) {
      const rect = triggerRef.current.getBoundingClientRect()
      setPos({ top: rect.bottom + 8, right: window.innerWidth - rect.right })
    }
    setMounted(true)
    requestAnimationFrame(() => setVisible(true))
  }
  const close = useCallback(() => {
    setVisible(false)
    timerRef.current = setTimeout(() => setMounted(false), 200)
  }, [])

  useEffect(() => () => clearTimeout(timerRef.current), [])

  // Click-outside: close when clicking outside both trigger and popover
  useEffect(() => {
    if (!mounted) return
    const handler = (e: MouseEvent) => {
      const target = e.target as Node
      if (
        triggerRef.current &&
        !triggerRef.current.contains(target) &&
        popoverRef.current &&
        !popoverRef.current.contains(target)
      ) {
        close()
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [mounted, close])

  return (
    <>
      <button
        ref={triggerRef}
        className="flex items-center gap-2 rounded-full outline-none ring-offset-background focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 bg-transparent border-0 cursor-pointer p-0"
        onClick={() => (mounted ? close() : open())}
        aria-label="User menu"
      >
        <Avatar className="h-7 w-7">
          <AvatarImage src={user?.image ?? undefined} alt={user?.name ?? 'User'} />
          <AvatarFallback className="text-xs font-medium">{initials}</AvatarFallback>
        </Avatar>
      </button>

      {mounted &&
        createPortal(
          <div
            ref={popoverRef}
            className={cn(
              'fixed z-50 w-56 overflow-hidden rounded-xl border border-[var(--glass-border)] bg-[var(--glass-bg)] shadow-xl backdrop-blur-2xl transition-all duration-200',
              visible ? 'scale-100 opacity-100' : 'scale-95 opacity-0'
            )}
            style={{ top: pos.top, right: pos.right }}
          >
            <div className="border-b border-border/30 px-3 py-2.5">
              {user?.name && <p className="truncate text-sm font-semibold">{user.name}</p>}
              {user?.email && <p className="truncate text-xs text-muted-foreground">{user.email}</p>}
            </div>
            <div className="p-1">
              <button
                className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm transition-colors hover:bg-accent"
                onClick={() => {
                  close()
                  router.push('/profile')
                }}
              >
                <User className="h-4 w-4" />
                Profile
              </button>
            </div>
            <div className="border-t border-border/30 p-1">
              <button
                className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm text-destructive transition-colors hover:bg-destructive/10"
                onClick={() => {
                  close()
                  signOut({ callbackUrl: '/login' })
                }}
              >
                <LogOut className="h-4 w-4" />
                Sign out
              </button>
            </div>
          </div>,
          document.body
        )}
    </>
  )
}
