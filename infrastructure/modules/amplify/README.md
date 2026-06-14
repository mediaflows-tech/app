# `amplify` Module

Provisions the AWS Amplify app, branch, and IAM role that serve the Next.js SSR frontend, and mirrors Cognito values into SSM Parameter Store for cold-start access.

> Top-level project README is at [`../../../README.md`](../../../README.md).

## Resources

- `aws_amplify_app.frontend` — Amplify app configured for SSR (`WEB_COMPUTE` platform)
- `aws_amplify_branch.main` — Tracked branch with auto-build enabled
- `aws_amplify_domain_association.main` — Custom domain registration (conditional on `custom_domain`)
- `aws_iam_role.amplify_ssr` — Execution role for both the Amplify service and SSR Lambda
- `aws_iam_role_policy.amplify_ssr` — Grants CloudWatch Logs write and SSM `GetParametersByPath` for the secrets path
- `aws_ssm_parameter.cognito_client_id` — Cognito client ID mirrored for SSR cold-start reads
- `aws_ssm_parameter.cognito_issuer` — Cognito OIDC issuer URL mirrored for SSR cold-start reads

## Inputs

| Variable               | Type     | Description                                                                          |
| ---------------------- | -------- | ------------------------------------------------------------------------------------ |
| `environment`          | `string` | Deployment environment                                                               |
| `project_name`         | `string` | Project name prefix (default: `mediaflows`)                                          |
| `aws_region`           | `string` | AWS region, used to scope SSM ARN in the SSR role policy (default: `ap-southeast-1`) |
| `repository`           | `string` | GitHub repository URL                                                                |
| `branch_name`          | `string` | Git branch to deploy (default: `main`)                                               |
| `domain_name`          | `string` | Apex domain — retained for backward compat, currently unused inside the module       |
| `custom_domain`        | `string` | Fully-qualified domain to register with Amplify; empty skips domain association      |
| `frontend_url`         | `string` | URL at which the frontend is served (injected as `NEXTAUTH_URL`)                     |
| `api_base_url`         | `string` | Backend API base URL                                                                 |
| `cognito_client_id`    | `string` | Cognito app client ID                                                                |
| `cognito_issuer`       | `string` | Cognito OIDC issuer URL                                                              |
| `cognito_user_pool_id` | `string` | Cognito User Pool ID                                                                 |
| `cdn_url`              | `string` | CDN URL for media assets (default: `""`)                                             |

## Outputs

| Output                           | Description                                                                    |
| -------------------------------- | ------------------------------------------------------------------------------ |
| `app_id`                         | Amplify app ID                                                                 |
| `app_arn`                        | Amplify app ARN                                                                |
| `default_domain`                 | Amplify-assigned default domain                                                |
| `branch_name`                    | Deployed branch name                                                           |
| `custom_domain_verification_dns` | DNS record for custom domain certificate verification (null if not configured) |

## Notes

- Sensitive secrets (`NEXTAUTH_SECRET`, `COGNITO_CLIENT_SECRET`) are **not** stored in Amplify environment variables. They are loaded at SSR cold-start via `frontend/src/instrumentation.ts` using `ssm:GetParametersByPath`, which avoids leaking them into the Amplify Console UI.
- The `custom_domain` guard exists because an old AWS account still holds the global Amplify domain claim for `example.com`. While the claim is in place, the root module uses Route 53 alias records pointing directly at Amplify's CloudFront distribution instead.
