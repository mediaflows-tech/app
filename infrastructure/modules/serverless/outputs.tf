# infrastructure/modules/serverless/outputs.tf
output "thumbnail_generator_arn" {
  description = "ThumbnailGenerator Lambda function ARN"
  value       = aws_lambda_function.thumbnail_generator.arn
}

output "content_moderator_arn" {
  description = "ContentModerator Lambda function ARN"
  value       = aws_lambda_function.content_moderator.arn
}

output "notification_dispatcher_arn" {
  description = "NotificationDispatcher Lambda function ARN"
  value       = aws_lambda_function.notification_dispatcher.arn
}

output "search_api_arn" {
  description = "SearchApi Lambda function ARN"
  value       = aws_lambda_function.search_api.arn
}

output "analytics_aggregator_arn" {
  description = "AnalyticsAggregator Lambda function ARN"
  value       = aws_lambda_function.analytics_aggregator.arn
}

output "trending_api_arn" {
  description = "TrendingApi Lambda function ARN"
  value       = aws_lambda_function.trending_api.arn
}

output "post_confirmation_group_assigner_arn" {
  description = "PostConfirmationGroupAssigner Lambda function ARN"
  value       = aws_lambda_function.post_confirmation_group_assigner.arn
}

output "post_confirmation_group_assigner_name" {
  description = "PostConfirmationGroupAssigner Lambda function name"
  value       = aws_lambda_function.post_confirmation_group_assigner.function_name
}

output "api_gateway_invoke_url" {
  description = "API Gateway invoke URL"
  value       = aws_api_gateway_stage.main.invoke_url
}

output "api_gateway_id" {
  description = "API Gateway REST API ID"
  value       = aws_api_gateway_rest_api.main.id
}

output "lambda_execution_role_arn" {
  description = "Lambda execution role ARN"
  value       = aws_iam_role.lambda_execution.arn
}

output "lambda_execution_role_name" {
  description = "Lambda execution role name (used for attaching inline policies from the root module)"
  value       = aws_iam_role.lambda_execution.name
}
