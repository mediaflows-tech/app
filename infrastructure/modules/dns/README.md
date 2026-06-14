# `dns` Module

Provisions the Route 53 hosted zone, two ACM certificates (regional and CloudFront/us-east-1), and an optional www-to-apex redirect using S3 + CloudFront.

> Top-level project README is at [`../../../README.md`](../../../README.md).

## Resources

- `aws_route53_zone.main` — Public hosted zone for the apex domain
- `aws_acm_certificate.regional` — Wildcard TLS certificate in `ap-southeast-1` for the ALB
- `aws_acm_certificate.cloudfront` — Wildcard TLS certificate in `us-east-1` for CloudFront distributions and the Cognito custom domain
- `aws_route53_record.cert_validation` — DNS CNAME records for ACM DNS validation (shared by both certs)
- `aws_acm_certificate_validation.regional` — Waits for the regional cert to become ISSUED
- `aws_acm_certificate_validation.cloudfront` — Waits for the CloudFront cert to become ISSUED
- `aws_s3_bucket.www_redirect` + `aws_s3_bucket_website_configuration.www_redirect` — S3 static-website redirect bucket for `www.<domain>` (conditional)
- `aws_cloudfront_distribution.www_redirect` — CloudFront distribution fronting the redirect bucket (conditional)
- `aws_route53_record.www` — Route 53 A alias for `www.<domain>` (conditional)

## Inputs

| Variable              | Type     | Description                                     |
| --------------------- | -------- | ----------------------------------------------- |
| `domain_name`         | `string` | Apex domain name (e.g. `example.com`)       |
| `environment`         | `string` | Deployment environment                          |
| `project_name`        | `string` | Project name prefix (default: `mediaflows`)     |
| `manage_www_redirect` | `bool`   | Create the www redirect stack (default: `true`) |

## Outputs

| Output                           | Description                                                   |
| -------------------------------- | ------------------------------------------------------------- |
| `zone_id`                        | Route 53 hosted zone ID                                       |
| `name_servers`                   | Route 53 name servers (update these at your registrar)        |
| `acm_certificate_arn_regional`   | Validated regional ACM cert ARN (for ALB)                     |
| `acm_certificate_arn_cloudfront` | Validated us-east-1 ACM cert ARN (for CloudFront and Cognito) |
| `domain_name`                    | Root domain name (pass-through)                               |

## Notes

- This module requires the `aws.us_east_1` provider alias in the calling module/root so the CloudFront certificate can be created in `us-east-1`.
- `manage_www_redirect` should be set to `false` when another AWS account still holds a CloudFront CNAME for `www.<domain>` — creating a conflicting distribution will fail.
- After first apply, update the domain registrar's name servers with the values from the `name_servers` output. ACM validation will not complete until Route 53 is authoritative.
