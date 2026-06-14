import { NextResponse } from 'next/server'
import { cognitoCall, computeSecretHash, type CognitoError } from '@/lib/cognito-server'

interface ConfirmBody {
  email?: string
  code?: string
}

export async function POST(req: Request) {
  let body: ConfirmBody
  try {
    body = (await req.json()) as ConfirmBody
  } catch {
    return NextResponse.json({ __type: 'InvalidParameterException' }, { status: 400 })
  }

  const email = body.email?.toLowerCase().trim() ?? ''
  const code = body.code?.trim() ?? ''

  if (!email || !code) {
    return NextResponse.json(
      { __type: 'InvalidParameterException', message: 'Missing email or code.' },
      { status: 400 }
    )
  }

  try {
    await cognitoCall('ConfirmSignUp', {
      ClientId: process.env.COGNITO_CLIENT_ID!,
      SecretHash: computeSecretHash(email),
      Username: email,
      ConfirmationCode: code
    })
    return NextResponse.json({ ok: true })
  } catch (err) {
    return NextResponse.json(err as CognitoError, { status: 400 })
  }
}
