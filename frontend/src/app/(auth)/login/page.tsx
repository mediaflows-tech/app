'use client'

import { Suspense, useEffect, useState } from 'react'
import { signIn } from 'next-auth/react'
import { useSearchParams, useRouter } from 'next/navigation'
import Link from 'next/link'
import toast from 'react-hot-toast'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'

// Maps Cognito error codes (from CredentialsSignin subclasses) to user-facing messages
function getErrorMessage(error?: string | null, code?: string | null): string {
  if (!error && !code) return ''
  if (code === 'user_not_confirmed')
    return 'Your email has not been confirmed yet. Please check your inbox for a confirmation link.'
  if (code === 'new_password_required') return 'You need to set a new password before signing in.'
  if (code === 'password_reset_required')
    return 'Your password has been reset by an administrator. Please reset your password.'
  if (error === 'CredentialsSignin') return 'Invalid email or password.'
  if (error === 'SessionRequired') return 'Please sign in to continue.'
  if (error) return 'An error occurred. Please try again.'
  return ''
}

function LoginForm() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const searchParams = useSearchParams()
  const router = useRouter()

  // Validate callbackUrl is same-origin to prevent open redirect (including //evil.com)
  let callbackUrl = '/dashboard'
  const rawCallback = searchParams.get('callbackUrl')
  if (rawCallback) {
    try {
      const parsed = new URL(rawCallback, window.location.origin)
      if (parsed.origin === window.location.origin) {
        callbackUrl = parsed.pathname + parsed.search
      }
    } catch {
      // Invalid URL — use default
    }
  }

  // Handle NextAuth error codes passed via URL (e.g. session expiry, OIDC errors)
  const urlError = searchParams.get('error')
  const urlCode = searchParams.get('code')

  // Success messages from registration/password reset flows
  const confirmed = searchParams.get('confirmed') === 'true'
  const reset = searchParams.get('reset') === 'true'
  const successMessage = confirmed
    ? 'Email verified successfully. You can now sign in.'
    : reset
      ? 'Password reset successfully. Sign in with your new password.'
      : ''

  // Surface a Cognito sign-in error as a toast. For states needing a follow-up
  // action (unconfirmed email, password reset) we keep the contextual link
  // inside a persistent toast the user can dismiss.
  function notifyError(error?: string | null, code?: string | null) {
    const message = getErrorMessage(error, code)
    if (!message) return

    const showConfirmLink = code === 'user_not_confirmed'
    const showResetLink = code === 'new_password_required' || code === 'password_reset_required'

    if (!showConfirmLink && !showResetLink) {
      toast.error(message, { id: 'login-error' })
      return
    }

    toast.error(
      (t) => (
        <div>
          <p>{message}</p>
          {showConfirmLink && (
            <Link
              href={`/confirm?email=${encodeURIComponent(email)}`}
              onClick={() => toast.dismiss(t.id)}
              className="mt-1 block underline underline-offset-4"
            >
              Resend confirmation email
            </Link>
          )}
          {showResetLink && (
            <Link
              href="/forgot-password"
              onClick={() => toast.dismiss(t.id)}
              className="mt-1 block underline underline-offset-4"
            >
              Reset your password
            </Link>
          )}
        </div>
      ),
      { id: 'login-error', duration: Infinity }
    )
  }

  // Surface messages passed via URL (session expiry, OIDC errors, post-register
  // or post-reset success) as toasts on mount.
  useEffect(() => {
    if (successMessage) toast.success(successMessage, { id: 'login-success' })
    if (urlError) notifyError(urlError, urlCode)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)

    try {
      const result = await signIn('credentials', {
        email,
        password,
        redirect: false
      })

      if (result?.error) {
        notifyError(result.error, result.code ?? null)
        setLoading(false)
        return
      }

      router.push(callbackUrl)
    } catch {
      toast.error('An unexpected error occurred. Please try again.', { id: 'login-error' })
      setLoading(false)
    }
  }

  return (
    <>
      <h1 className="text-2xl font-bold tracking-tight">Sign in</h1>
      <p className="mt-1 mb-6 text-sm text-muted-foreground">Welcome back to MediaFlows</p>

      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <Label
            htmlFor="email"
            className="text-[0.6875rem] font-semibold uppercase tracking-[0.06em] text-muted-foreground"
          >
            Email
          </Label>
          <Input
            id="email"
            type="email"
            placeholder="you@example.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            autoComplete="email"
            autoFocus
            disabled={loading}
            className="h-10 rounded-lg border-transparent bg-muted px-3.5 text-[0.9375rem] focus-visible:border-ring focus-visible:bg-background"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label
            htmlFor="password"
            className="text-[0.6875rem] font-semibold uppercase tracking-[0.06em] text-muted-foreground"
          >
            Password
          </Label>
          <Input
            id="password"
            type="password"
            placeholder="Enter your password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            autoComplete="current-password"
            disabled={loading}
            className="h-10 rounded-lg border-transparent bg-muted px-3.5 text-[0.9375rem] focus-visible:border-ring focus-visible:bg-background"
          />
        </div>

        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Checkbox id="remember" />
            <Label htmlFor="remember" className="text-sm text-muted-foreground font-normal">
              Remember me
            </Label>
          </div>
          <Link href="/forgot-password" className="text-sm text-muted-foreground hover:underline">
            Forgot password?
          </Link>
        </div>

        <Button type="submit" disabled={loading} className="w-full rounded-full" size="lg">
          {loading ? 'Signing in...' : 'Sign In'}
        </Button>
      </form>

      <p className="mt-5 text-center text-[0.8125rem] text-muted-foreground">
        Don&apos;t have an account?{' '}
        <Link href="/register" className="font-semibold text-foreground hover:underline">
          Sign up
        </Link>
      </p>
    </>
  )
}

function LoginSkeleton() {
  return (
    <div className="animate-pulse space-y-4">
      <div className="h-7 w-32 rounded bg-muted" />
      <div className="h-4 w-56 rounded bg-muted" />
      <div className="space-y-3 pt-2">
        <div className="h-3 w-12 rounded bg-muted" />
        <div className="h-10 rounded-lg bg-muted" />
        <div className="h-3 w-16 rounded bg-muted" />
        <div className="h-10 rounded-lg bg-muted" />
      </div>
      <div className="h-10 rounded-full bg-muted" />
    </div>
  )
}

export default function LoginPage() {
  return (
    <Suspense fallback={<LoginSkeleton />}>
      <LoginForm />
    </Suspense>
  )
}
