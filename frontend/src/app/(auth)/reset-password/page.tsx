'use client'

import { Suspense, useState, useRef, useCallback } from 'react'
import { useRouter, useSearchParams, redirect } from 'next/navigation'
import Link from 'next/link'
import { TriangleAlert } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { confirmPasswordReset, mapAmplifyError } from '@/lib/auth-client'

const PASSWORD_RULES = [
  'At least 8 characters',
  'Uppercase and lowercase letters',
  'At least one number',
  'At least one symbol'
]

function ResetPasswordForm() {
  const [code, setCode] = useState(['', '', '', '', '', ''])
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const codeRef = useRef(code)
  codeRef.current = code
  const inputRefs = useRef<(HTMLInputElement | null)[]>([])
  const searchParams = useSearchParams()
  const router = useRouter()
  const email = searchParams.get('email') ?? ''

  // Guard: redirect if no email param
  if (!email) {
    redirect('/forgot-password')
  }

  const handleChange = useCallback((index: number, value: string) => {
    if (value && !/^\d$/.test(value)) return
    setCode((prev) => {
      const next = [...prev]
      next[index] = value
      return next
    })
    if (value && index < 5) {
      inputRefs.current[index + 1]?.focus()
    }
  }, [])

  const handleKeyDown = useCallback((index: number, e: React.KeyboardEvent) => {
    if (e.key === 'Backspace' && !codeRef.current[index] && index > 0) {
      inputRefs.current[index - 1]?.focus()
    }
  }, [])

  const handlePaste = useCallback((e: React.ClipboardEvent) => {
    e.preventDefault()
    const paste = e.clipboardData.getData('text').trim()
    const digits = paste.replace(/\D/g, '').slice(0, 6)
    if (!digits) return
    setCode((prev) => {
      const next = [...prev]
      for (let i = 0; i < digits.length; i++) {
        next[i] = digits[i]
      }
      return next
    })
    inputRefs.current[Math.min(digits.length, 5)]?.focus()
  }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const fullCode = code.join('')

    if (fullCode.length !== 6) {
      setError('Please enter the 6-digit code.')
      return
    }
    if (newPassword !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }

    setError('')
    setLoading(true)

    try {
      await confirmPasswordReset(email, fullCode, newPassword)
      router.push('/login?reset=true')
    } catch (err) {
      setError(mapAmplifyError(err, 'Password reset failed. Please try again.'))
      setLoading(false)
    }
  }

  return (
    <>
      <h1 className="text-2xl font-bold tracking-tight">Reset password</h1>
      <p className="mt-1 mb-6 text-sm text-muted-foreground">
        Enter the code sent to <span className="font-medium text-foreground">{email}</span> and choose a new password.
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
            id="code-label"
            className="text-[0.6875rem] font-semibold uppercase tracking-[0.06em] text-muted-foreground"
          >
            Verification Code
          </Label>
          <div className="flex justify-center gap-2" role="group" aria-labelledby="code-label">
            {code.map((digit, i) => (
              <input
                key={i}
                ref={(el) => {
                  inputRefs.current[i] = el
                }}
                type="text"
                inputMode="numeric"
                maxLength={1}
                value={digit}
                onChange={(e) => handleChange(i, e.target.value)}
                onKeyDown={(e) => handleKeyDown(i, e)}
                onPaste={handlePaste}
                autoFocus={i === 0}
                aria-label={`Digit ${i + 1} of 6`}
                disabled={loading}
                className="size-12 rounded-lg border border-transparent bg-muted text-center text-xl font-semibold outline-none transition-colors focus:border-ring focus:bg-background focus:ring-3 focus:ring-ring/50 disabled:opacity-50"
              />
            ))}
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label
            htmlFor="newPassword"
            className="text-[0.6875rem] font-semibold uppercase tracking-[0.06em] text-muted-foreground"
          >
            New Password
          </Label>
          <Input
            id="newPassword"
            type="password"
            placeholder="Create a new password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            required
            autoComplete="new-password"
            disabled={loading}
            className="h-10 rounded-lg border-transparent bg-muted px-3.5 text-[0.9375rem] focus-visible:border-ring focus-visible:bg-background"
          />
          <ul className="mt-1 space-y-0.5">
            {PASSWORD_RULES.map((rule) => (
              <li key={rule} className="flex items-center gap-1.5 text-[0.6875rem] text-muted-foreground">
                <span className="size-1 rounded-full bg-muted-foreground/50" />
                {rule}
              </li>
            ))}
          </ul>
        </div>

        <div className="flex flex-col gap-1.5">
          <Label
            htmlFor="confirmPassword"
            className="text-[0.6875rem] font-semibold uppercase tracking-[0.06em] text-muted-foreground"
          >
            Confirm New Password
          </Label>
          <Input
            id="confirmPassword"
            type="password"
            placeholder="Re-enter new password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            required
            autoComplete="new-password"
            disabled={loading}
            className="h-10 rounded-lg border-transparent bg-muted px-3.5 text-[0.9375rem] focus-visible:border-ring focus-visible:bg-background"
          />
        </div>

        <Button type="submit" disabled={loading} className="w-full rounded-full" size="lg">
          {loading ? 'Resetting...' : 'Reset Password'}
        </Button>
      </form>

      <p className="mt-5 text-center text-[0.8125rem] text-muted-foreground">
        <Link href="/forgot-password" className="font-semibold text-foreground hover:underline">
          Didn&apos;t receive a code? Request a new one
        </Link>
      </p>
    </>
  )
}

function ResetSkeleton() {
  return (
    <div className="animate-pulse space-y-4">
      <div className="h-7 w-48 rounded bg-muted" />
      <div className="h-4 w-64 rounded bg-muted" />
      <div className="flex justify-center gap-2 py-4">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="size-12 rounded-lg bg-muted" />
        ))}
      </div>
      <div className="h-10 rounded-lg bg-muted" />
      <div className="h-10 rounded-lg bg-muted" />
      <div className="h-10 rounded-full bg-muted" />
    </div>
  )
}

export default function ResetPasswordPage() {
  return (
    <Suspense fallback={<ResetSkeleton />}>
      <ResetPasswordForm />
    </Suspense>
  )
}
