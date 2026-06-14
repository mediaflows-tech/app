# `cdn` Module

Provisions the CloudFront distribution that serves media assets from S3, with an Origin Access Control (OAC) policy that locks down direct S3 access.

> Top-level project README is at [`../../../README.md`](../../../README.md).

## Resources

- `aws_cloudfront_origin_access_control.s3_oac` — OAC for SigV4-signed requests from CloudFront to S3
- `aws_cloudfront_distribution.media` — CloudFront distribution with aggressive caching for assets and thumbnails
- `aws_s3_bucket_policy.cloudfront_oac` — S3 bucket policy allowing reads only from this distribution

## Inputs

| Variable                         | Type     | Description                                                                         |
| -------------------------------- | -------- | ----------------------------------------------------------------------------------- |
| `environment`                    | `string` | Deployment environment                                                              |
| `project_name`                   | `string` | Project name prefix (default: `mediaflows`)                                         |
| `s3_bucket_id`                   | `string` | S3 bucket ID (name) for the origin                                                  |
| `s3_bucket_arn`                  | `string` | S3 bucket ARN for the bucket policy                                                 |
| `s3_bucket_regional_domain_name` | `string` | S3 regional domain name used as the CloudFront origin                               |
| `custom_domain`                  | `string` | Custom domain alias (e.g. `cdn.example.com`); empty = CloudFront default domain |
| `acm_certificate_arn`            | `string` | ACM certificate ARN in `us-east-1`; required when `custom_domain` is set            |

## Outputs

| Output                       | Description                               |
| ---------------------------- | ----------------------------------------- |
| `cloudfront_distribution_id` | CloudFront distribution ID                |
| `cloudfront_domain_name`     | CloudFront domain name                    |
| `cloudfront_arn`             | CloudFront distribution ARN               |
| `cloudfront_hosted_zone_id`  | Hosted zone ID for Route 53 alias records |

## Notes

- Default cache TTL is 1 day; thumbnails (`/thumbnails/*`) have a minimum TTL of 1 day and a max of 1 year.
- Price class `PriceClass_200` covers Asia-Pacific, North America, and Europe.
- The S3 bucket must have **public access blocked**; all reads are routed through CloudFront via OAC.
