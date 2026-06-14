# infrastructure/modules/dns/variables.tf
variable "domain_name" {
  description = "Root domain name (e.g. example.com)"
  type        = string
}

variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "mediaflows"
}

variable "manage_www_redirect" {
  description = "Create www.<domain> redirect (S3 + CloudFront). Disable when www.<domain> CloudFront CNAME is held by another account and can't be released — site users just use the apex/app subdomain."
  type        = bool
  default     = true
}
