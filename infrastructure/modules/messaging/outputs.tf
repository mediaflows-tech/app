# infrastructure/modules/messaging/outputs.tf
output "media_processing_queue_arn" {
  description = "Media processing SQS queue ARN"
  value       = aws_sqs_queue.media_processing.arn
}

output "media_processing_queue_url" {
  description = "Media processing SQS queue URL"
  value       = aws_sqs_queue.media_processing.id
}

output "approval_workflow_queue_arn" {
  description = "Approval workflow FIFO queue ARN"
  value       = aws_sqs_queue.approval_workflow.arn
}

output "notification_queue_arn" {
  description = "Notification SQS queue ARN"
  value       = aws_sqs_queue.notification.arn
}

output "asset_uploaded_topic_arn" {
  description = "Asset uploaded SNS topic ARN"
  value       = aws_sns_topic.asset_uploaded.arn
}

output "content_flagged_topic_arn" {
  description = "Content flagged SNS topic ARN"
  value       = aws_sns_topic.content_flagged.arn
}

output "review_completed_topic_arn" {
  description = "Review completed SNS topic ARN"
  value       = aws_sns_topic.review_completed.arn
}

output "asset_published_topic_arn" {
  description = "Asset published SNS topic ARN"
  value       = aws_sns_topic.asset_published.arn
}

output "system_alerts_topic_arn" {
  description = "System alerts SNS topic ARN"
  value       = aws_sns_topic.system_alerts.arn
}

output "content_moderation_queue_arn" {
  description = "Content moderation SQS queue ARN"
  value       = aws_sqs_queue.content_moderation.arn
}

output "event_bus_name" {
  description = "MediaFlows custom EventBridge event bus name"
  value       = aws_cloudwatch_event_bus.mediaflows.name
}
