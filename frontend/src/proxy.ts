import { auth } from '@/lib/auth'
import { NextResponse } from 'next/server'
import type { UserRole } from '@/types/auth'

const roleRoutes: Record<string, UserRole[]> = {
  '/admin': ['SystemAdmin'],
  '/creator': ['SystemAdmin', 'ContentCreator'],
  '/review': ['SystemAdmin', 'Editor'],
  '/schedule': ['SystemAdmin', 'Editor']
}

const authPages = ['/login', '/register', '/confirm', '/forgot-password', '/reset-password']

export const proxy = auth((req) => {
  const { pathname } = req.nextUrl

  // Landing page is always public
  if (pathname === '/') {
    return NextResponse.next()
  }

  // Auth pages: redirect authenticated users to dashboard
  if (authPages.some((p) => pathname.startsWith(p))) {
    if (req.auth) return NextResponse.redirect(new URL('/dashboard', req.url))
    return NextResponse.next()
  }

  // All other routes require authentication
  if (!req.auth) {
    const loginUrl = new URL('/login', req.url)
    loginUrl.searchParams.set('callbackUrl', pathname)
    return NextResponse.redirect(loginUrl)
  }

  const userRole = req.auth.user.role as UserRole | undefined

  for (const [routePrefix, allowedRoles] of Object.entries(roleRoutes)) {
    if (pathname.startsWith(routePrefix) && !allowedRoles.includes(userRole ?? 'Viewer')) {
      return NextResponse.redirect(new URL('/dashboard', req.url))
    }
  }

  return NextResponse.next()
})

export const config = {
  matcher: [
    '/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp|mp4|webm|mp3|wav|ogg)$|api/).*)'
  ]
}
