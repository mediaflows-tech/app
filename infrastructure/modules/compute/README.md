# `compute` Module

Provisions the Elastic Beanstalk application and environment that runs the ASP.NET Core API, along with the IAM roles, instance profile, and (optionally) a self-signed ACM certificate for the ALB HTTPS listener.

> Top-level project README is at [`../../../README.md`](../../../README.md).

## Resources

- `aws_iam_role.eb_ec2_role` — EC2 instance role with S3, DynamoDB, Cognito, SNS, SQS, Rekognition, SSM, CloudWatch, and Cost Explorer permissions
- `aws_iam_instance_profile.eb_ec2_profile` — Instance profile wrapping the EC2 role
- `aws_iam_role.eb_service_role` — EB service role with enhanced health and managed-update policies
- `aws_elastic_beanstalk_application.main` — EB application with lifecycle management (max 10 versions)
- `aws_elastic_beanstalk_environment.main` — EB WebServer environment on Amazon Linux 2023 / .NET 8 with ALB, rolling deployments, X-Ray, and enhanced health reporting
- `tls_private_key.alb` + `tls_self_signed_cert.alb` + `aws_acm_certificate.alb` — Self-signed certificate for ALB HTTPS when no ACM cert is provided (dev only)
- `data.aws_lb.eb_alb` — Data source to read the ALB created by EB (for Route 53 alias records)

## Inputs

| Variable                   | Type           | Description                                                            |
| -------------------------- | -------------- | ---------------------------------------------------------------------- |
| `environment`              | `string`       | Deployment environment                                                 |
| `project_name`             | `string`       | Project name prefix (default: `mediaflows`)                            |
| `vpc_id`                   | `string`       | VPC ID                                                                 |
| `app_subnet_ids`           | `list(string)` | Private subnets for EC2 instances                                      |
| `public_subnet_ids`        | `list(string)` | Public subnets for the ALB                                             |
| `app_security_group_id`    | `string`       | Security group for EC2 instances                                       |
| `alb_security_group_id`    | `string`       | Security group for the ALB                                             |
| `instance_type`            | `string`       | EC2 instance type (default: `t3.micro`)                                |
| `min_instances`            | `number`       | ASG minimum size (default: `1`)                                        |
| `max_instances`            | `number`       | ASG maximum size (default: `4`)                                        |
| `db_connection_string`     | `string`       | PostgreSQL connection string (sensitive)                               |
| `s3_bucket_name`           | `string`       | S3 bucket name for media assets                                        |
| `cloudfront_domain`        | `string`       | CloudFront domain for assets; empty falls back to direct S3 (will 403) |
| `cognito_authority`        | `string`       | Cognito OIDC authority URL                                             |
| `cognito_client_id`        | `string`       | Cognito app client ID                                                  |
| `cognito_client_secret`    | `string`       | Cognito app client secret (sensitive)                                  |
| `cognito_domain`           | `string`       | Cognito hosted UI domain URL                                           |
| `cognito_user_pool_id`     | `string`       | Cognito User Pool ID                                                   |
| `acm_certificate_arn`      | `string`       | ACM certificate ARN for ALB HTTPS; empty = self-signed                 |
| `app_domain`               | `string`       | Custom domain for the app; empty = no custom domain                    |
| `asset_uploaded_topic_arn` | `string`       | SNS topic ARN for Rekognition pipeline; empty disables Rekognition     |

## Outputs

| Output                    | Description                           |
| ------------------------- | ------------------------------------- |
| `eb_application_name`     | EB application name                   |
| `eb_environment_name`     | EB environment name                   |
| `eb_environment_url`      | EB environment endpoint URL           |
| `eb_environment_cname`    | EB environment CNAME                  |
| `eb_instance_profile_arn` | EC2 instance profile ARN              |
| `alb_dns_name`            | ALB DNS name for Route 53 alias       |
| `alb_zone_id`             | ALB hosted zone ID for Route 53 alias |

## Notes

- The solution stack (`64bit Amazon Linux 2023 v3.10.1 running .NET 8`) is pinned to a specific minor version. AWS retires old versions regularly; check availability with `aws elasticbeanstalk list-available-solution-stacks` when a deploy fails with "No Solution Stack named …".
- Autoscaling triggers on CPU: scale-out at 70%, scale-in at 30%.
- The ALB health check is routed to `/api/health` (the API has no handler at `/`).
- The EC2 role has `AmazonSSMManagedInstanceCore` so the EB instance is reachable via Systems Manager (`aws ssm send-command`). This is what lets you run `psql` from inside the VPC against the private RDS (e.g. for seeding or TRUNCATE) — without it the SSM agent can't register and remote commands silently no-op.
