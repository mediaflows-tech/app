# `storage` Module

Provisions the S3 bucket that stores all media assets, with versioning, server-side encryption, CORS for presigned-URL uploads, and lifecycle rules for quarantine and version cleanup.

> Top-level project README is at [`../../../README.md`](../../../README.md).

## Resources

- `aws_s3_bucket.media_assets` — Primary media assets bucket (name: `<project>-assets-<env>-<account_id>`)
- `aws_s3_bucket_public_access_block.media_assets` — Blocks all public access; assets are served via CloudFront OAC only
- `aws_s3_bucket_versioning.media_assets` — Versioning enabled for accidental-deletion recovery
- `aws_s3_bucket_server_side_encryption_configuration.media_assets` — AES-256 SSE with Bucket Key enabled
- `aws_s3_bucket_cors_configuration.media_assets` — Allows `GET`, `HEAD`, `PUT` from configured origins for presigned-URL direct uploads
- `aws_s3_bucket_lifecycle_configuration.media_assets` — Three rules: quarantine tier to Glacier after 30 days / delete after 90; abort incomplete multipart uploads after 7 days; expire non-current versions after 30 days

## Inputs

| Variable                       | Type           | Description                                                                                               |
| ------------------------------ | -------------- | --------------------------------------------------------------------------------------------------------- |
| `environment`                  | `string`       | Deployment environment                                                                                    |
| `project_name`                 | `string`       | Project name prefix (default: `mediaflows`)                                                               |
| `content_moderation_queue_arn` | `string`       | SQS queue ARN for content moderation (used by the CloudFront OAC bucket policy wired by the `cdn` module) |
| `cors_allowed_origins`         | `list(string)` | Origins permitted for presigned-URL uploads (default: `localhost` dev origins)                            |

## Outputs

| Output                        | Description                                        |
| ----------------------------- | -------------------------------------------------- |
| `bucket_name`                 | S3 bucket name                                     |
| `bucket_arn`                  | S3 bucket ARN                                      |
| `bucket_id`                   | S3 bucket ID (same as name)                        |
| `bucket_regional_domain_name` | Regional domain name used as the CloudFront origin |

## Notes

- The bucket name is suffixed with the AWS account ID to ensure global uniqueness and avoid collisions with buckets from a previous account that AWS has not yet released.
- The CloudFront OAC bucket policy (granting CloudFront read access) is applied by the `cdn` module, not here, because it depends on the CloudFront distribution ARN.
- Files uploaded to `quarantine/` are automatically tiered to Glacier after 30 days and deleted after 90 days.
