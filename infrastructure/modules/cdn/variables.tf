# infrastructure/modules/cdn/variables.tf
variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "mediaflows"
}

variable "s3_bucket_id" {
  description = "S3 bucket ID for the origin"
  type        = string
}

variable "s3_bucket_arn" {
  description = "S3 bucket ARN for the bucket policy"
  type        = string
}

variable "s3_bucket_regional_domain_name" {
  description = "S3 bucket regional domain name"
  type        = string
}

variable "custom_domain" {
  description = "Custom domain for CloudFront (e.g. cdn.example.com). Empty = no custom domain."
  type        = string
  default     = ""
}

variable "acm_certificate_arn" {
  description = "ACM certificate ARN in us-east-1 for CloudFront. Required when custom_domain is set."
  type        = string
  default     = ""
}
