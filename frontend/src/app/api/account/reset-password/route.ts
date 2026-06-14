import { NextResponse } from 'next/server'
import { cognitoCall, computeSecretHash, type CognitoError } from '@/lib/cognito-server'

interface ResetBody {
  email?: string
  code?: string
  newPassword?: string
}

export async function POST(req: Request) {
  let body: ResetBody
  try {
    body = (await req.json()) as ResetBody
  } catch {
    return NextResponse.json({ __type: 'InvalidParameterException' }, { status: 400 })
  }

  const email = body.email?.toLowerCase().trim() ?? ''
  const code = body.code?.trim() ?? ''
  const newPassword = body.newPassword ?? ''

  if (!email || !code || !newPassword) {
    return NextResponse.json(
      { __type: 'InvalidParameterException', message: 'Missing required fields.' },
      { status: 400 }
    )
  }

  try {
    await cognitoCall('ConfirmForgotPassword', {
      ClientId: process.env.COGNITO_CLIENT_ID!,
      SecretHash: computeSecretHash(email),
      Username: email,
      ConfirmationCode: code,
      Password: newPassword
    })
    return NextResponse.json({ ok: true })
  } catch (err) {
    return NextResponse.json(err as CognitoError, { status: 400 })
  }
}
