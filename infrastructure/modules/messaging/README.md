# `messaging` Module

Provisions the SQS queues, SNS topics, and EventBridge custom event bus that form the async event backbone of the MediaFlows platform.

> Top-level project README is at [`../../../README.md`](../../../README.md).

## Resources

- `aws_sqs_queue.media_processing` — Standard queue for S3-upload events feeding `ThumbnailGenerator` (visibility 360s, DLQ after 3 failures)
- `aws_sqs_queue.approval_workflow` — FIFO queue for ordered approval-workflow messages
- `aws_sqs_queue.notification` — Standard queue for user notification dispatch
- `aws_sqs_queue.content_moderation` — Standard queue for `ContentModerator` Lambda (triggered by S3 events)
- `aws_sqs_queue.media_processing_dlq` — DLQ for media processing failures (14-day retention)
- `aws_sqs_queue.approval_workflow_dlq` — FIFO DLQ for approval workflow failures
- `aws_sqs_queue.notification_dlq` — DLQ for notification failures
- `aws_sns_topic.asset_uploaded` — Fan-out topic for upload events → media-processing queue
- `aws_sns_topic.content_flagged` — Topic for moderation alerts to admins
- `aws_sns_topic.review_completed` — Topic for review decisions → notification queue
- `aws_sns_topic.asset_published` — Topic for scheduled-publish events
- `aws_sns_topic.system_alerts` — Topic targeted by CloudWatch alarms
- `aws_sns_topic_subscription.*` — Wires `asset_uploaded` → media-processing, `review_completed` → notification queue, and `review_completed` → email (when `notification_email` is set)
- `aws_sqs_queue_policy.*` — Policies allowing S3 and SNS to send messages to the appropriate queues
- `aws_cloudwatch_event_bus.mediaflows` — Custom EventBridge event bus for domain events

## Inputs

| Variable        | Type     | Description                                                     |
| --------------- | -------- | --------------------------------------------------------------- |
| `environment`        | `string` | Deployment environment                                                     |
| `project_name`       | `string` | Project name prefix (default: `mediaflows`)                                |
| `s3_bucket_arn`      | `string` | S3 bucket ARN used to scope queue policies for S3 notifications             |
| `notification_email` | `string` | Email subscribed to `review_completed` for review-decision mail (optional) |

## Outputs

| Output                         | Description                       |
| ------------------------------ | --------------------------------- |
| `media_processing_queue_arn`   | Media processing SQS queue ARN    |
| `media_processing_queue_url`   | Media processing SQS queue URL    |
| `approval_workflow_queue_arn`  | Approval workflow FIFO queue ARN  |
| `notification_queue_arn`       | Notification queue ARN            |
| `asset_uploaded_topic_arn`     | Asset uploaded SNS topic ARN      |
| `content_flagged_topic_arn`    | Content flagged SNS topic ARN     |
| `review_completed_topic_arn`   | Review completed SNS topic ARN    |
| `asset_published_topic_arn`    | Asset published SNS topic ARN     |
| `system_alerts_topic_arn`      | System alerts SNS topic ARN       |
| `content_moderation_queue_arn` | Content moderation queue ARN      |
| `event_bus_name`               | Custom EventBridge event bus name |

## Notes

- EventBridge rules that **target Lambda functions** are defined in the root `infrastructure/main.tf`, not here, to avoid a circular dependency between the `messaging` and `serverless` modules.
- All main queues use long polling (`receive_wait_time_seconds = 20`) to reduce empty-receive costs.
