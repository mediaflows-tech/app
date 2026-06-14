// NOTE: Do NOT add `import 'server-only'` here. This module is transitively
// reachable from `lib/auth.ts`, which is imported by `proxy.ts` (middleware)
// and by `lib/api.ts` (via a dynamic import that Turbopack still traces
// statically). `server-only` throws at import time in every non-Server-
// Component context, so adding it here breaks the middleware and client-SSR
// bundles. Keep this file server-side by convention — the network call
// below would fail in the browser anyway (no COGNITO_CLIENT_SECRET env var
// and no Node `crypto` module).
import { createHmac } from 'crypto'

// SECRET_HASH is required on every Cognito API call when the App Client has a
// client secret. It is an HMAC-SHA256 of (username + client_id) keyed with the
// client secret, base64-encoded.
// https://docs.aws.amazon.com/cognito/latest/developerguide/signing-up-users-in-your-app.html#cognito-user-pools-computing-secret-hash
export function computeSecretHash(username: string): string {
  return createHmac('sha256', process.env.COGNITO_CLIENT_SECRET!)
    .update(username + process.env.COGNITO_CLIENT_ID!)
    .digest('base64')
}

export function getCognitoRegion(): string {
  return process.env.COGNITO_ISSUER!.match(/cognito-idp\.(.+?)\.amazonaws/)?.[1] ?? 'ap-southeast-1'
}

export interface CognitoError {
  __type: string
  message?: string
}

// Generic Cognito action caller. Throws a `CognitoError` on non-2xx responses
// AND on network/fetch failures so route handlers can forward a typed error to
// the client. Network failures surface as `__type: 'NetworkError'`.
export async function cognitoCall<T = unknown>(action: string, body: Record<string, unknown>): Promise<T> {
  let response: Response
  try {
    response = await fetch(`https://cognito-idp.${getCognitoRegion()}.amazonaws.com/`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-amz-json-1.1',
        'X-Amz-Target': `AWSCognitoIdentityProviderService.${action}`
      },
      body: JSON.stringify(body)
    })
  } catch {
    const netErr: CognitoError = {
      __type: 'NetworkError',
      message: 'Unable to reach authentication service.'
    }
    throw netErr
  }

  const data = await response.json().catch(() => ({}))
  if (!response.ok) {
    const err: CognitoError = {
      __type: (data.__type as string)?.split('#').pop() ?? 'UnknownError',
      message: data.message as string | undefined
    }
    throw err
  }
  return data as T
}
