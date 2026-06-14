# `serverless` Module

Provisions the Lambda execution role and seven .NET 8 Lambda functions that handle async media processing, content moderation, notifications, search, trending reads, analytics aggregation, and Cognito post-confirmation group assignment. Also creates the API Gateway REST API exposing search and trending endpoints.

> Top-level project README is at [`../../../README.md`](../../../README.md).

## Resources

- `aws_iam_role.lambda_execution` — Shared execution role with VPC access, X-Ray, S3, DynamoDB, Rekognition, SNS, SQS, and CloudWatch Metrics permissions
- `aws_lambda_function.thumbnail_generator` — Generates thumbnails from uploaded media (SQS-triggered via `media_processing` queue)
- `aws_lambda_function.content_moderator` — Runs Rekognition moderation on new uploads (SQS-triggered via `content_moderation` queue; VPC-attached for RDS access)
- `aws_lambda_function.notification_dispatcher` — Dispatches user notifications (EventBridge-triggered from root module)
- `aws_lambda_function.search_api` — Full-text search handler (API Gateway + VPC-attached for RDS)
- `aws_lambda_function.analytics_aggregator` — Aggregates DynamoDB view counters into trending data (EventBridge scheduled cron, nightly)
- `aws_lambda_function.trending_api` — Serves the trending endpoint by walking back up to 7 day-buckets (API Gateway)
- `aws_lambda_function.post_confirmation_group_assigner` — Assigns self-registered Cognito users to the Viewer group
- `aws_lambda_event_source_mapping.thumbnail_sqs` — SQS trigger for ThumbnailGenerator
- `aws_lambda_event_source_mapping.content_moderator_sqs` — SQS trigger for ContentModerator
- `aws_api_gateway_rest_api.main` — Regional REST API
- `aws_api_gateway_resource.*` + `aws_api_gateway_method.*` + `aws_api_gateway_integration.*` — Routes for `/api/search` (GET) and `/api/analytics/trending` (GET → TrendingApi)
- `aws_api_gateway_deployment.main` + `aws_api_gateway_stage.main` — Deployed stage with X-Ray enabled
- `aws_lambda_permission.*` — API Gateway invocation permissions for SearchApi and TrendingApi

## Inputs

| Variable                       | Type           | Description                                                  |
| ------------------------------ | -------------- | ------------------------------------------------------------ |
| `environment`                  | `string`       | Deployment environment                                       |
| `project_name`                 | `string`       | Project name prefix (default: `mediaflows`)                  |
| `s3_bucket_name`               | `string`       | S3 bucket name for media assets                              |
| `s3_bucket_arn`                | `string`       | S3 bucket ARN                                                |
| `lambda_sg_id`                 | `string`       | Security group ID for VPC-attached Lambda functions          |
| `app_subnet_ids`               | `list(string)` | Private app subnet IDs for VPC-attached functions            |
| `db_connection_string`         | `string`       | PostgreSQL connection string (sensitive)                     |
| `content_flagged_topic_arn`    | `string`       | SNS topic ARN for content-flagged events                     |
| `notification_topic_arn`       | `string`       | SNS topic ARN for user notifications                         |
| `content_moderation_queue_arn` | `string`       | SQS queue ARN for content moderation                         |
| `media_processing_queue_arn`   | `string`       | SQS queue ARN for media processing                           |
| `lambda_memory_size`           | `number`       | Memory for most Lambda functions in MB (default: `512`)      |
| `lambda_timeout`               | `number`       | Timeout for most Lambda functions in seconds (default: `60`) |

## Outputs

| Output                                  | Description                                                                     |
| --------------------------------------- | ------------------------------------------------------------------------------- |
| `thumbnail_generator_arn`               | ThumbnailGenerator Lambda ARN                                                   |
| `content_moderator_arn`                 | ContentModerator Lambda ARN                                                     |
| `notification_dispatcher_arn`           | NotificationDispatcher Lambda ARN                                               |
| `search_api_arn`                        | SearchApi Lambda ARN                                                            |
| `analytics_aggregator_arn`              | AnalyticsAggregator Lambda ARN                                                  |
| `trending_api_arn`                      | TrendingApi Lambda ARN                                                          |
| `post_confirmation_group_assigner_arn`  | PostConfirmationGroupAssigner Lambda ARN                                        |
| `post_confirmation_group_assigner_name` | PostConfirmationGroupAssigner Lambda function name                              |
| `api_gateway_invoke_url`                | API Gateway stage invoke URL                                                    |
| `api_gateway_id`                        | API Gateway REST API ID                                                         |
| `lambda_execution_role_arn`             | Lambda execution role ARN                                                       |
| `lambda_execution_role_name`            | Lambda execution role name (for attaching inline policies from the root module) |

## Notes

- All functions are initialised from a placeholder zip at `terraform apply` time. Actual code is deployed by the GitHub Actions CI/CD pipeline using `aws lambda update-function-code`.
- All functions run on `dotnet8` / `arm64` for best price-performance.
- The Cognito `AdminAddUserToGroup` IAM permission and Lambda invocation permission for `PostConfirmationGroupAssigner` are defined in the **root module** (`infrastructure/main.tf`) to avoid a circular dependency between the `auth` and `serverless` modules.
