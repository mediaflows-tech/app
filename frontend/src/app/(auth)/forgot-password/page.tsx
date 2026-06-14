'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import Link from 'next/link'
import { TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { requestPasswordReset, mapAmplifyError } from '@/lib/auth-client'

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const router = useRouter()

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setError('')
    setLoading(true)

    try {
      await requestPasswordReset(email)
      router.push(`/reset-password?email=${encodeURIComponent(email)}`)
    } catch (err) {
      // Always show generic message to prevent user enumeration
      const name = err instanceof Error ? err.name : ''
      if (name === 'UserNotFoundException') {
        // Silently redirect as if success — don't reveal account existence
        router.push(`/reset-password?email=${encodeURIComponent(email)}`)
        return
      }
      setError(mapAmplifyError(err, 'Failed to send verification code. Please try again.'))
      setLoading(false)
    }
  }

  return (
    <>
      <h1 className="text-2xl font-bold tracking-tight">Forgot password?</h1>
      <p className="mt-1 mb-6 text-sm text-muted-foreground">
        Enter your email and we&apos;ll send you a verification code to reset your password.
      </p>

      {error && (
        <div className="mb-4 flex items-start gap-2 rounded-lg bg-destructive/10 px-3 py-2 text-sm text-destructive">
          <TriangleAlert className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <p>{error}</p>
        </div>
      )}

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

        <Button type="submit" disabled={loading} className="w-full rounded-full" size="lg">
          {loading ? 'Sending...' : 'Send Verification Code'}
        </Button>

        <Button
          type="button"
          variant="secondary"
          className="w-full rounded-full"
          size="lg"
          onClick={() => router.push('/login')}
          disabled={loading}
        >
          Back to Sign In
        </Button>
      </form>
    </>
  )
}
