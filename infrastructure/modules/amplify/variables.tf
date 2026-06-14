# infrastructure/modules/amplify/variables.tf
variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "mediaflows"
}

variable "aws_region" {
  description = "AWS region — used to scope the SSM ARN in the SSR role policy"
  type        = string
  default     = "ap-southeast-1"
}

variable "repository" {
  description = "GitHub repository URL"
  type        = string
}

variable "branch_name" {
  description = "Git branch to deploy"
  type        = string
  default     = "main"
}

variable "domain_name" {
  description = "Apex domain (e.g. example.com) — retained for backward compat, currently unused inside the module. Use custom_domain for the actual Amplify domain registration."
  type        = string
  default     = ""
}

variable "custom_domain" {
  description = "Fully-qualified custom domain to register with Amplify (e.g. web.example.com). Empty string skips aws_amplify_domain_association."
  type        = string
  default     = ""
}

variable "frontend_url" {
  description = "URL at which the frontend is served — injected as NEXTAUTH_URL at build time. Example: https://web.example.com."
  type        = string
}

variable "api_base_url" {
  description = "Backend API base URL"
  type        = string
}

variable "cognito_client_id" {
  description = "Cognito app client ID"
  type        = string
}

variable "cognito_issuer" {
  description = "Cognito OIDC issuer URL"
  type        = string
}

variable "cognito_user_pool_id" {
  description = "Cognito User Pool ID — exposed to the frontend as NEXT_PUBLIC_COGNITO_USER_POOL_ID"
  type        = string
}

variable "cdn_url" {
  description = "CDN URL for media assets"
  type        = string
  default     = ""
}
