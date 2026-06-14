import NextAuth, { CredentialsSignin } from 'next-auth'
import Cognito from 'next-auth/providers/cognito'
import Credentials from 'next-auth/providers/credentials'
import type { JWT } from '@auth/core/jwt'
import type { UserRole } from '@/types/auth'
import { computeSecretHash, getCognitoRegion } from './cognito-server'

// NextAuth v5 propagates `code` to the client via signIn({ redirect: false })
class UserNotConfirmed extends CredentialsSignin {
  code = 'user_not_confirmed'
}

class NewPasswordRequired extends CredentialsSignin {
  code = 'new_password_required'
}

class PasswordResetRequired extends CredentialsSignin {
  code = 'password_reset_required'
}

function decodeJwtPayload(token: string): Record<string, unknown> | null {
  try {
    return JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString())
  } catch {
    return null
  }
}

// Credentials sessions must refresh via REFRESH_TOKEN_AUTH (not the OIDC token endpoint).
// SECRET_HASH for refresh uses the user's sub (not email) per AWS docs.
async function refreshViaInitiateAuth(typedToken: JWT): Promise<JWT> {
  const response = await fetch(`https://cognito-idp.${getCognitoRegion()}.amazonaws.com/`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/x-amz-json-1.1',
      'X-Amz-Target': 'AWSCognitoIdentityProviderService.InitiateAuth'
    },
    body: JSON.stringify({
      AuthFlow: 'REFRESH_TOKEN_AUTH',
      ClientId: process.env.COGNITO_CLIENT_ID!,
      AuthParameters: {
        REFRESH_TOKEN: typedToken.refreshToken,
        SECRET_HASH: computeSecretHash(typedToken.id) // sub for refresh
      }
    })
  })

  const data = await response.json()
  if (!response.ok || !data.AuthenticationResult) throw data

  const { AccessToken, ExpiresIn } = data.AuthenticationResult
  return {
    ...typedToken,
    accessToken: AccessToken,
    expiresAt: Math.floor(Date.now() / 1000 + ExpiresIn),
    // REFRESH_TOKEN_AUTH does not return a new refresh token
    refreshToken: typedToken.refreshToken
  }
}

// OIDC sessions (Cognito hosted UI) use the standard token endpoint.
async function refreshViaOidc(typedToken: JWT): Promise<JWT> {
  const oidcConfig = await fetch(`${process.env.COGNITO_ISSUER}/.well-known/openid-configuration`).then((r) => r.json())

  const tokenResponse = await fetch(oidcConfig.token_endpoint as string, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      client_id: process.env.COGNITO_CLIENT_ID!,
      client_secret: process.env.COGNITO_CLIENT_SECRET!,
      grant_type: 'refresh_token',
      refresh_token: typedToken.refreshToken!
    })
  })

  const tokens = (await tokenResponse.json()) as {
    access_token: string
    expires_in: number
    refresh_token?: string
  }
  if (!tokenResponse.ok) throw tokens

  return {
    ...typedToken,
    accessToken: tokens.access_token,
    expiresAt: Math.floor(Date.now() / 1000 + tokens.expires_in),
    refreshToken: tokens.refresh_token ?? typedToken.refreshToken
  }
}

export const { handlers, auth, signIn, signOut } = NextAuth({
  providers: [
    Cognito({
      clientId: process.env.COGNITO_CLIENT_ID!,
      clientSecret: process.env.COGNITO_CLIENT_SECRET!,
      issuer: process.env.COGNITO_ISSUER!,
      authorization: { params: { scope: 'openid email profile' } }
    }),
    Credentials({
      credentials: {
        email: { label: 'Email', type: 'email' },
        password: { label: 'Password', type: 'password' }
      },
      async authorize(credentials) {
        const email = (credentials.email as string).toLowerCase().trim()
        const password = credentials.password as string
        if (!email || !password) return null

        const response = await fetch(`https://cognito-idp.${getCognitoRegion()}.amazonaws.com/`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/x-amz-json-1.1',
            'X-Amz-Target': 'AWSCognitoIdentityProviderService.InitiateAuth'
          },
          body: JSON.stringify({
            AuthFlow: 'USER_PASSWORD_AUTH',
            ClientId: process.env.COGNITO_CLIENT_ID!,
            AuthParameters: {
              USERNAME: email,
              PASSWORD: password,
              SECRET_HASH: computeSecretHash(email)
            }
          })
        })

        const data = await response.json()

        // Handle Cognito challenges (no AuthenticationResult)
        if (data.ChallengeName === 'NEW_PASSWORD_REQUIRED') {
          throw new NewPasswordRequired()
        }

        // Handle Cognito API errors
        if (!response.ok || !data.AuthenticationResult) {
          const errorType = data.__type as string | undefined
          if (errorType === 'UserNotConfirmedException') throw new UserNotConfirmed()
          if (errorType === 'PasswordResetRequiredException') throw new PasswordResetRequired()
          // NotAuthorizedException, UserNotFoundException (masked) → generic failure
          return null
        }

        const { AccessToken, IdToken, RefreshToken, ExpiresIn } = data.AuthenticationResult
        const idPayload = decodeJwtPayload(IdToken)
        if (!idPayload) return null

        const groups = (idPayload['cognito:groups'] as string[]) ?? []

        return {
          id: idPayload.sub as string,
          email: idPayload.email as string,
          name: (idPayload.name as string) ?? (idPayload.email as string),
          accessToken: AccessToken,
          refreshToken: RefreshToken,
          expiresAt: Math.floor(Date.now() / 1000 + ExpiresIn),
          role: mapCognitoGroupToRole(groups)
        }
      }
    })
  ],
  session: { strategy: 'jwt' },
  pages: { signIn: '/login', error: '/login' },
  callbacks: {
    async jwt({ token, account, profile, user }) {
      const typedToken = token as JWT

      // Initial sign-in via OIDC (Cognito hosted UI / register callback)
      if (account?.provider === 'cognito' && profile) {
        const groups = (profile as Record<string, unknown>)['cognito:groups'] as string[] | undefined
        return {
          ...typedToken,
          id: profile.sub as string,
          role: mapCognitoGroupToRole(groups),
          accessToken: account.access_token as string,
          refreshToken: account.refresh_token as string,
          expiresAt: account.expires_at as number,
          provider: 'cognito' as const
        }
      }

      // Initial sign-in via Credentials (direct Cognito InitiateAuth)
      if (account?.provider === 'credentials' && user) {
        const u = user as Record<string, unknown>
        return {
          ...typedToken,
          id: u.id as string,
          role: u.role as UserRole,
          accessToken: u.accessToken as string,
          refreshToken: u.refreshToken as string,
          expiresAt: u.expiresAt as number,
          provider: 'credentials' as const
        }
      }

      // Token not expired — return as-is
      if (Date.now() < typedToken.expiresAt * 1000) return typedToken

      // Token expired — try refresh
      if (!typedToken.refreshToken) {
        return { ...typedToken, error: 'RefreshTokenError' as const }
      }

      try {
        if (typedToken.provider === 'credentials') {
          return await refreshViaInitiateAuth(typedToken)
        }
        return await refreshViaOidc(typedToken)
      } catch (error) {
        console.error('Error refreshing access token', error)
        return { ...typedToken, error: 'RefreshTokenError' as const }
      }
    },
    async session({ session, token }) {
      const typedToken = token as JWT
      session.user.id = typedToken.id
      session.user.role = typedToken.role
      session.user.accessToken = typedToken.accessToken
      session.error = typedToken.error
      return session
    }
  }
})

function mapCognitoGroupToRole(groups?: string[]): UserRole {
  if (!groups || groups.length === 0) return 'Viewer'
  if (groups.includes('SystemAdmin')) return 'SystemAdmin'
  if (groups.includes('Editor')) return 'Editor'
  if (groups.includes('ContentCreator')) return 'ContentCreator'
  return 'Viewer'
}
