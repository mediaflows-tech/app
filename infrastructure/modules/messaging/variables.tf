# infrastructure/modules/messaging/variables.tf
variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "mediaflows"
}

variable "s3_bucket_arn" {
  description = "S3 bucket ARN for event notification policy"
  type        = string
}

variable "notification_email" {
  description = "Email address subscribed to the review-completed SNS topic. Empty = no email subscription is created."
  type        = string
  default     = ""
}
