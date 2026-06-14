import type { DefaultSession } from 'next-auth'

export type UserRole = 'SystemAdmin' | 'ContentCreator' | 'Editor' | 'Viewer'

declare module 'next-auth' {
  interface Session {
    user: {
      id: string
      role: UserRole
      accessToken: string
    } & DefaultSession['user']
    error?: 'RefreshTokenError'
  }
}

declare module '@auth/core/jwt' {
  interface JWT {
    id: string
    role: UserRole
    accessToken: string
    refreshToken?: string
    expiresAt: number
    provider?: 'credentials' | 'cognito'
    error?: 'RefreshTokenError'
  }
}
