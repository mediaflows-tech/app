# infrastructure/modules/auth/variables.tf
variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "mediaflows"
}

variable "callback_urls" {
  description = "Allowed callback URLs for the app client"
  type        = list(string)
}

variable "logout_urls" {
  description = "Allowed logout URLs for the app client"
  type        = list(string)
}

variable "custom_domain" {
  description = "Custom domain for Cognito hosted UI (e.g. auth.example.com). Empty = prefix domain."
  type        = string
  default     = ""
}

variable "acm_certificate_arn" {
  description = "ACM certificate ARN in us-east-1 for Cognito custom domain. Required when custom_domain is set."
  type        = string
  default     = ""
}

variable "post_confirmation_lambda_arn" {
  description = "ARN of the post-confirmation Lambda trigger. Wired from the serverless module. Empty string disables the trigger (used during bootstrap)."
  type        = string
  default     = ""
}
