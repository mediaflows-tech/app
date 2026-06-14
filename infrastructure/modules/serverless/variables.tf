# infrastructure/modules/serverless/variables.tf
variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "mediaflows"
}

variable "s3_bucket_name" {
  description = "S3 bucket name for media assets"
  type        = string
}

variable "s3_bucket_arn" {
  description = "S3 bucket ARN"
  type        = string
}

variable "lambda_sg_id" {
  description = "Security group ID for Lambda functions"
  type        = string
}

variable "app_subnet_ids" {
  description = "Subnet IDs for VPC-attached Lambda functions"
  type        = list(string)
}

variable "db_connection_string" {
  description = "PostgreSQL connection string for Lambda"
  type        = string
  sensitive   = true
}

variable "content_flagged_topic_arn" {
  description = "SNS topic ARN for content-flagged notifications"
  type        = string
}

variable "notification_topic_arn" {
  description = "SNS topic ARN for general notifications"
  type        = string
}

variable "content_moderation_queue_arn" {
  description = "SQS queue ARN for content moderation"
  type        = string
}

variable "media_processing_queue_arn" {
  description = "SQS queue ARN for media processing"
  type        = string
}

variable "lambda_memory_size" {
  description = "Lambda function memory in MB"
  type        = number
  default     = 512
}

variable "lambda_timeout" {
  description = "Lambda function timeout in seconds"
  type        = number
  default     = 60
}
