// Client-side auth helpers. These POST to internal Next.js route handlers
// under /api/account/*, which run server-side and compute the Cognito
// SECRET_HASH (required because our App Client has a client secret).
// Never call Cognito directly from the browser — it cannot hold the secret.

interface CognitoErrorPayload {
  __type?: string
  message?: string
}

class CognitoClientError extends Error {
  readonly code: string
  constructor(payload: CognitoErrorPayload) {
    super(payload.message ?? payload.__type ?? 'Request failed')
    this.name = payload.__type ?? 'CognitoError'
    this.code = payload.__type ?? 'UnknownError'
  }
}

async function postAccount(path: string, body: Record<string, unknown>) {
  const response = await fetch(`/api/account/${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body)
  })

  if (!response.ok) {
    const payload = (await response.json().catch(() => ({}))) as CognitoErrorPayload
    throw new CognitoClientError(payload)
  }
  return response.json()
}

export async function registerUser(email: string, password: string, displayName: string) {
  return postAccount('register', { email, password, displayName })
}

export async function confirmRegistration(email: string, code: string) {
  return postAccount('confirm', { email, code })
}

export async function resendConfirmationCode(email: string) {
  return postAccount('resend-code', { email })
}

export async function requestPasswordReset(email: string) {
  return postAccount('forgot-password', { email })
}

export async function confirmPasswordReset(email: string, code: string, newPassword: string) {
  return postAccount('reset-password', { email, code, newPassword })
}

// Maps Cognito error names to user-friendly messages.
// Falls back to a generic message for unmapped errors.
//
// NOTE: `NotAuthorizedException` and `UserNotFoundException` are intentionally
// omitted. They have flow-specific meanings (login vs confirm vs forgot) and
// any shared mapping would be misleading in at least one flow. Each caller
// provides its own fallback, and forgot-password handles `UserNotFoundException`
// locally for user-enumeration masking.
const ERROR_MAP: Record<string, string> = {
  UsernameExistsException: 'An account with this email already exists.',
  InvalidPasswordException: 'Password does not meet the requirements.',
  CodeMismatchException: 'Invalid verification code. Please check and try again.',
  ExpiredCodeException: 'Verification code has expired. Please request a new one.',
  LimitExceededException: 'Too many attempts. Please wait a moment and try again.',
  InvalidParameterException: 'Please check your input and try again.',
  TooManyRequestsException: 'Too many requests. Please wait a moment and try again.',
  UserLambdaValidationException: 'Registration blocked by validation rules.',
  NetworkError: "Can't reach the authentication service. Check your connection and try again."
}

export function mapAmplifyError(err: unknown, fallback: string): string {
  if (err instanceof CognitoClientError) {
    return ERROR_MAP[err.code] ?? fallback
  }
  if (err instanceof Error && ERROR_MAP[err.name]) {
    return ERROR_MAP[err.name]
  }
  return fallback
}
