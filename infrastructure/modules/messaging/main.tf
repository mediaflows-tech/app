# infrastructure/modules/messaging/main.tf

# ──────────────────────────────────────────────────
# SQS Dead Letter Queues
# ──────────────────────────────────────────────────
resource "aws_sqs_queue" "media_processing_dlq" {
  name                      = "${var.project_name}-media-processing-dlq-${var.environment}"
  message_retention_seconds = 1209600 # 14 days

  tags = {
    Name = "${var.project_name}-media-processing-dlq-${var.environment}"
  }
}

resource "aws_sqs_queue" "approval_workflow_dlq" {
  name                        = "${var.project_name}-approval-workflow-dlq-${var.environment}.fifo"
  fifo_queue                  = true
  content_based_deduplication = true
  message_retention_seconds   = 1209600

  tags = {
    Name = "${var.project_name}-approval-workflow-dlq-${var.environment}"
  }
}

resource "aws_sqs_queue" "notification_dlq" {
  name                      = "${var.project_name}-notification-dlq-${var.environment}"
  message_retention_seconds = 1209600

  tags = {
    Name = "${var.project_name}-notification-dlq-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# SQS Queues
# ──────────────────────────────────────────────────

# Media Processing Queue — Standard, triggered by S3 uploads
resource "aws_sqs_queue" "media_processing" {
  name                       = "${var.project_name}-media-processing-${var.environment}"
  visibility_timeout_seconds = 360   # 6x Lambda timeout (60s)
  message_retention_seconds  = 86400 # 1 day
  receive_wait_time_seconds  = 20    # Long polling

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.media_processing_dlq.arn
    maxReceiveCount     = 3
  })

  tags = {
    Name = "${var.project_name}-media-processing-${var.environment}"
  }
}

# Approval Workflow Queue — FIFO for ordered processing
resource "aws_sqs_queue" "approval_workflow" {
  name                        = "${var.project_name}-approval-workflow-${var.environment}.fifo"
  fifo_queue                  = true
  content_based_deduplication = true
  visibility_timeout_seconds  = 180
  message_retention_seconds   = 86400
  receive_wait_time_seconds   = 20

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.approval_workflow_dlq.arn
    maxReceiveCount     = 5
  })

  tags = {
    Name = "${var.project_name}-approval-workflow-${var.environment}"
  }
}

# Notification Queue — Standard
resource "aws_sqs_queue" "notification" {
  name                       = "${var.project_name}-notification-${var.environment}"
  visibility_timeout_seconds = 60
  message_retention_seconds  = 86400
  receive_wait_time_seconds  = 20

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.notification_dlq.arn
    maxReceiveCount     = 3
  })

  tags = {
    Name = "${var.project_name}-notification-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# SNS Topics
# ──────────────────────────────────────────────────

# Asset Uploaded — fan-out to multiple SQS consumers
resource "aws_sns_topic" "asset_uploaded" {
  name = "${var.project_name}-asset-uploaded-${var.environment}"

  tags = {
    Name = "${var.project_name}-asset-uploaded-${var.environment}"
  }
}

# Content Flagged — alert admins on moderation issues
resource "aws_sns_topic" "content_flagged" {
  name = "${var.project_name}-content-flagged-${var.environment}"

  tags = {
    Name = "${var.project_name}-content-flagged-${var.environment}"
  }
}

# Review Completed — notify creators
resource "aws_sns_topic" "review_completed" {
  name = "${var.project_name}-review-completed-${var.environment}"

  tags = {
    Name = "${var.project_name}-review-completed-${var.environment}"
  }
}

# Asset Published — ScheduledPublisher Lambda notifications
resource "aws_sns_topic" "asset_published" {
  name = "${var.project_name}-asset-published-${var.environment}"

  tags = {
    Name = "${var.project_name}-asset-published-${var.environment}"
  }
}

# System Alerts — CloudWatch alarms target
resource "aws_sns_topic" "system_alerts" {
  name = "${var.project_name}-system-alerts-${var.environment}"

  tags = {
    Name = "${var.project_name}-system-alerts-${var.environment}"
  }
}

# SNS topic policy: allow S3 (the media-assets bucket) to publish
# asset-uploaded events into the topic. Subscribers (both Lambdas)
# receive the raw S3 event JSON because raw_message_delivery=true is
# set on each subscription below.
resource "aws_sns_topic_policy" "asset_uploaded_s3" {
  arn = aws_sns_topic.asset_uploaded.arn

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "AllowS3Publish"
        Effect    = "Allow"
        Principal = { Service = "s3.amazonaws.com" }
        Action    = "sns:Publish"
        Resource  = aws_sns_topic.asset_uploaded.arn
        Condition = {
          ArnEquals = { "aws:SourceArn" = var.s3_bucket_arn }
        }
      }
    ]
  })
}

resource "aws_sns_topic_subscription" "asset_uploaded_to_processing" {
  topic_arn            = aws_sns_topic.asset_uploaded.arn
  protocol             = "sqs"
  endpoint             = aws_sqs_queue.media_processing.arn
  raw_message_delivery = true
}

resource "aws_sns_topic_subscription" "asset_uploaded_to_moderation" {
  topic_arn            = aws_sns_topic.asset_uploaded.arn
  protocol             = "sqs"
  endpoint             = aws_sqs_queue.content_moderation.arn
  raw_message_delivery = true
}

resource "aws_sns_topic_subscription" "review_completed_to_notification" {
  topic_arn = aws_sns_topic.review_completed.arn
  protocol  = "sqs"
  endpoint  = aws_sqs_queue.notification.arn
}

# Email delivery for review-decision notifications. Created only when an
# address is configured; SNS sends a confirmation email that must be accepted.
resource "aws_sns_topic_subscription" "review_completed_email" {
  count     = var.notification_email != "" ? 1 : 0
  topic_arn = aws_sns_topic.review_completed.arn
  protocol  = "email"
  endpoint  = var.notification_email
}

resource "aws_sqs_queue_policy" "media_processing_sns" {
  queue_url = aws_sqs_queue.media_processing.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowS3Notification"
        Effect = "Allow"
        Principal = {
          Service = "s3.amazonaws.com"
        }
        Action   = "sqs:SendMessage"
        Resource = aws_sqs_queue.media_processing.arn
        Condition = {
          ArnEquals = {
            "aws:SourceArn" = var.s3_bucket_arn
          }
        }
      },
      {
        Sid    = "AllowSNSNotification"
        Effect = "Allow"
        Principal = {
          Service = "sns.amazonaws.com"
        }
        Action   = "sqs:SendMessage"
        Resource = aws_sqs_queue.media_processing.arn
        Condition = {
          ArnEquals = {
            "aws:SourceArn" = aws_sns_topic.asset_uploaded.arn
          }
        }
      }
    ]
  })
}

# Content moderation SQS queue — dedicated queue for ContentModerator Lambda
resource "aws_sqs_queue" "content_moderation" {
  name                       = "${var.project_name}-content-moderation-${var.environment}"
  visibility_timeout_seconds = 360
  message_retention_seconds  = 86400
  receive_wait_time_seconds  = 20

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.media_processing_dlq.arn
    maxReceiveCount     = 3
  })

  tags = {
    Name = "${var.project_name}-content-moderation-${var.environment}"
  }
}

resource "aws_sqs_queue_policy" "content_moderation_s3" {
  queue_url = aws_sqs_queue.content_moderation.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "AllowS3ContentModeration"
        Effect    = "Allow"
        Principal = { Service = "s3.amazonaws.com" }
        Action    = "sqs:SendMessage"
        Resource  = aws_sqs_queue.content_moderation.arn
        Condition = {
          ArnEquals = { "aws:SourceArn" = var.s3_bucket_arn }
        }
      },
      {
        Sid       = "AllowSNSContentModeration"
        Effect    = "Allow"
        Principal = { Service = "sns.amazonaws.com" }
        Action    = "sqs:SendMessage"
        Resource  = aws_sqs_queue.content_moderation.arn
        Condition = {
          ArnEquals = { "aws:SourceArn" = aws_sns_topic.asset_uploaded.arn }
        }
      }
    ]
  })
}

resource "aws_sqs_queue_policy" "notification_sns" {
  queue_url = aws_sqs_queue.notification.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowSNSNotification"
        Effect = "Allow"
        Principal = {
          Service = "sns.amazonaws.com"
        }
        Action   = "sqs:SendMessage"
        Resource = aws_sqs_queue.notification.arn
        Condition = {
          ArnEquals = {
            "aws:SourceArn" = aws_sns_topic.review_completed.arn
          }
        }
      }
    ]
  })
}

# ──────────────────────────────────────────────────
# EventBridge Custom Event Bus
# ──────────────────────────────────────────────────
# NOTE: EventBridge rules that target Lambda functions are defined
# in the ROOT main.tf to avoid circular dependency between
# messaging and serverless modules.

resource "aws_cloudwatch_event_bus" "mediaflows" {
  name = "${var.project_name}-events-${var.environment}"

  tags = {
    Name = "${var.project_name}-event-bus-${var.environment}"
  }
}
