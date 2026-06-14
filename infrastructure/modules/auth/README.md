# `auth` Module

Provisions the Cognito User Pool, hosted UI domain, four role-based user groups, and the OIDC app client used for authentication across the MediaFlows platform.

> Top-level project README is at [`../../../README.md`](../../../README.md).

## Resources

- `aws_cognito_user_pool.main` — User pool with email-based login, password policy, and optional post-confirmation Lambda trigger
- `aws_cognito_user_pool_domain.prefix` — Prefix-based hosted UI domain (dev / no custom domain)
- `aws_cognito_user_pool_domain.custom` — Custom domain for the hosted UI (prod, conditional)
- `aws_cognito_user_group.system_admin` — SystemAdmin group (precedence 1)
- `aws_cognito_user_group.content_creator` — ContentCreator group (precedence 2)
- `aws_cognito_user_group.editor` — Editor group (precedence 3)
- `aws_cognito_user_group.viewer` — Viewer group (precedence 4, default for self-registered users)
- `aws_cognito_user_pool_client.web_app` — OIDC Authorization Code + PKCE client with client secret

## Inputs

| Variable                       | Type           | Description                                                             |
| ------------------------------ | -------------- | ----------------------------------------------------------------------- |
| `environment`                  | `string`       | Deployment environment                                                  |
| `project_name`                 | `string`       | Project name prefix (default: `mediaflows`)                             |
| `callback_urls`                | `list(string)` | Allowed OAuth callback URLs                                             |
| `logout_urls`                  | `list(string)` | Allowed logout redirect URLs                                            |
| `custom_domain`                | `string`       | Custom domain for Cognito hosted UI (empty = prefix domain)             |
| `acm_certificate_arn`          | `string`       | ACM certificate ARN in `us-east-1` required when `custom_domain` is set |
| `post_confirmation_lambda_arn` | `string`       | ARN of the Lambda post-confirmation trigger; empty disables the trigger |

## Outputs

| Output                                     | Description                                                          |
| ------------------------------------------ | -------------------------------------------------------------------- |
| `user_pool_id`                             | Cognito User Pool ID                                                 |
| `user_pool_arn`                            | Cognito User Pool ARN                                                |
| `client_id`                                | App client ID                                                        |
| `client_secret`                            | App client secret (sensitive)                                        |
| `user_pool_domain`                         | Hosted UI domain name                                                |
| `domain_url`                               | Full hosted UI URL                                                   |
| `authority`                                | OIDC authority URL                                                   |
| `issuer`                                   | OIDC issuer URL                                                      |
| `cognito_custom_domain_cloudfront`         | CloudFront domain for Cognito custom domain (empty if prefix domain) |
| `cognito_custom_domain_cloudfront_zone_id` | CloudFront hosted zone ID for Cognito custom domain                  |

## Notes

- The `post_confirmation_lambda_arn` is intentionally optional so the initial `terraform apply` can succeed before the Lambda is deployed. The wiring (Lambda permission + user pool trigger) is completed by the root module.
- Token validity: access/ID tokens expire in 60 minutes; refresh tokens in 30 days.
