# infrastructure/variables.tf
variable "aws_region" {
  description = "AWS region for all resources"
  type        = string
  default     = "ap-southeast-1"
}

variable "environment" {
  description = "Deployment environment (dev or prod)"
  type        = string
  validation {
    condition     = contains(["dev", "prod"], var.environment)
    error_message = "Environment must be 'dev' or 'prod'."
  }
}

variable "project_name" {
  description = "Project name used for resource naming"
  type        = string
  default     = "mediaflows"
}

# Database
variable "db_instance_class" {
  description = "RDS instance class"
  type        = string
  default     = "db.t3.micro"
}

variable "db_allocated_storage" {
  description = "RDS allocated storage in GB"
  type        = number
  default     = 20
}

variable "db_username" {
  description = "RDS master username"
  type        = string
  default     = "mediaflows_admin"
  sensitive   = true
}

# Compute
variable "eb_instance_type" {
  description = "Elastic Beanstalk EC2 instance type"
  type        = string
  default     = "t3.micro"
}

variable "eb_min_instances" {
  description = "Minimum number of EB instances"
  type        = number
  default     = 1
}

variable "eb_max_instances" {
  description = "Maximum number of EB instances"
  type        = number
  default     = 4
}

# Auth
variable "cognito_callback_urls" {
  description = "Cognito allowed callback URLs"
  type        = list(string)
  default = [
    "https://localhost:5001/signin-oidc",
    "http://localhost:3000/api/auth/callback/cognito",
    "https://example.com/api/auth/callback/cognito"
  ]
}

variable "cognito_logout_urls" {
  description = "Cognito allowed logout URLs"
  type        = list(string)
  default = [
    "https://localhost:5001/signout-callback-oidc",
    "http://localhost:3000",
    "https://example.com"
  ]
}

# Storage
variable "cors_allowed_origins" {
  description = "Allowed origins for browser-to-S3 uploads"
  type        = list(string)
  default = [
    "http://localhost:5000",
    "https://localhost:5001",
    "http://localhost:3000",
    "https://example.com"
  ]
}

# Lambda
variable "lambda_memory_size" {
  description = "Default Lambda function memory in MB"
  type        = number
  default     = 512
}

variable "lambda_timeout" {
  description = "Default Lambda function timeout in seconds"
  type        = number
  default     = 60
}

# Notifications
variable "notification_email" {
  description = "Email subscribed to the review-completed SNS topic for out-of-band review-decision emails. SNS sends a confirmation email that must be accepted. Empty = no subscription."
  type        = string
  default     = ""
}

# DNS
variable "domain_name" {
  description = "Custom domain name (e.g. example.com). Empty = no custom domain."
  type        = string
  default     = ""
}

variable "manage_www_redirect" {
  description = "Create www.<domain> → apex redirect via S3 + CloudFront. Disable when www CNAME is held by another account that can't release it."
  type        = bool
  default     = true
}

# Amplify
variable "github_owner" {
  description = "GitHub owner/org that owns the repo — propagated from the bootstrap stack"
  type        = string
}

variable "github_repo" {
  description = "GitHub repo name (without owner) — propagated from the bootstrap stack"
  type        = string
}

# Cutover
variable "amplify_cutover" {
  description = "Set to true to point root domain to Amplify instead of EB. Use for DNS cutover."
  type        = bool
  default     = false
}

variable "amplify_cloudfront_dns" {
  description = "Amplify Hosting CloudFront distribution DNS name (e.g. dxxxxxx.cloudfront.net). Required when amplify_cutover = true. Decouples the apex Route53 alias from the amplify Terraform module so the cutover can be done without managing the Amplify app via Terraform."
  type        = string
  default     = ""
}

# ──────────────────────────────────────────────────
# Services on/off (stop/start orchestration)
# ──────────────────────────────────────────────────
variable "services_enabled" {
  description = "Master switch — when false, destroys EB env, RDS, NAT GW, and Route53 records that alias the ALB. Toggle via CI/automation; avoid flipping it by hand mid-apply."
  type        = bool
  default     = true
}

variable "rds_restore_snapshot_id" {
  description = "If non-empty, the RDS instance is restored from this snapshot ID on create. Only honored at create time — passing on subsequent applies is a no-op."
  type        = string
  default     = ""
}
