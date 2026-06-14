# infrastructure/modules/compute/variables.tf
variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "mediaflows"
}

variable "vpc_id" {
  description = "VPC ID"
  type        = string
}

variable "app_subnet_ids" {
  description = "Private app subnet IDs for EC2 instances"
  type        = list(string)
}

variable "public_subnet_ids" {
  description = "Public subnet IDs for the ALB"
  type        = list(string)
}

variable "app_security_group_id" {
  description = "Security group ID for app instances"
  type        = string
}

variable "alb_security_group_id" {
  description = "Security group ID for the ALB"
  type        = string
}

variable "instance_type" {
  description = "EC2 instance type"
  type        = string
  default     = "t3.micro"
}

variable "min_instances" {
  description = "Minimum number of instances"
  type        = number
  default     = 1
}

variable "max_instances" {
  description = "Maximum number of instances"
  type        = number
  default     = 4
}

variable "db_connection_string" {
  description = "PostgreSQL connection string"
  type        = string
  sensitive   = true
}

variable "s3_bucket_name" {
  description = "S3 bucket name for media assets"
  type        = string
}

variable "cloudfront_domain" {
  description = "CloudFront domain serving the assets bucket. Empty falls back to the direct S3 URL — which will 403 because the bucket blocks public access."
  type        = string
  default     = ""
}

variable "cognito_authority" {
  description = "Cognito OIDC authority URL"
  type        = string
}

variable "cognito_client_id" {
  description = "Cognito app client ID"
  type        = string
}

variable "cognito_client_secret" {
  description = "Cognito app client secret"
  type        = string
  sensitive   = true
}

variable "cognito_domain" {
  description = "Cognito hosted UI domain URL"
  type        = string
}

variable "cognito_user_pool_id" {
  description = "Cognito User Pool ID"
  type        = string
}

variable "acm_certificate_arn" {
  description = "ACM certificate ARN for ALB HTTPS. Empty = use self-signed cert."
  type        = string
  default     = ""
}

variable "app_domain" {
  description = "Custom domain for the app (e.g. example.com). Empty = no custom domain."
  type        = string
  default     = ""
}

variable "asset_uploaded_topic_arn" {
  description = "SNS topic ARN for asset-uploaded events (Rekognition pipeline)"
  type        = string
  default     = ""
}

variable "enabled" {
  description = "When false, the EB application + environment + ALB lookup are not created. IAM roles and instance profile stay up (free, near-zero cost) to avoid IAM-role-replacement churn on every start."
  type        = bool
  default     = true
}

variable "event_bus_name" {
  description = "Custom EventBridge bus name the API publishes review notification events to"
  type        = string
  default     = ""
}
