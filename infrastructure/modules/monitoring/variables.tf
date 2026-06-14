# infrastructure/modules/monitoring/variables.tf
variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "mediaflows"
}

variable "system_alerts_topic_arn" {
  description = "SNS topic ARN for system alert notifications"
  type        = string
}

variable "eb_environment_name" {
  description = "Elastic Beanstalk environment name for metrics"
  type        = string
}

variable "media_processing_queue_name" {
  description = "SQS media processing queue name for metrics"
  type        = string
  default     = ""
}

variable "lambda_function_names" {
  description = "List of Lambda function names to monitor"
  type        = list(string)
  default     = []
}

variable "eb_enabled" {
  description = "When false, EB-dependent monitoring resources (high_cpu alarm, dashboard) are not created. Wired from services_enabled at root."
  type        = bool
  default     = true
}
