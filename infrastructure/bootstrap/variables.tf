# infrastructure/bootstrap/variables.tf

variable "aws_region" {
  description = "AWS region for the state bucket and SSM parameters"
  type        = string
  default     = "ap-southeast-1"
}

variable "project_name" {
  description = "Project name used for resource naming"
  type        = string
  default     = "mediaflows"
}

variable "environment" {
  description = "Deployment environment (dev or prod)"
  type        = string
  validation {
    condition     = contains(["dev", "prod"], var.environment)
    error_message = "Environment must be 'dev' or 'prod'."
  }
}

variable "github_owner" {
  description = "GitHub owner/org that owns the repo"
  type        = string
}

variable "github_repo" {
  description = "GitHub repo name (without owner)"
  type        = string
}

# ── Operator-supplied secrets seeded into SSM SecureString ────
# All three are `sensitive = true` and excluded from terraform show/plan output.
# Storing them as ignore_changes-on-value in main.tf means: on initial create,
# whatever value is set here goes into SSM; on subsequent applies, the SSM
# value is left alone regardless of what the variable says. So once SSM has
# real values, you can remove the lines from terraform.tfvars and operate with
# the defaults here.

variable "db_password" {
  description = "Initial RDS master password — seeded into SSM at /mediaflows/<env>/db-password on first apply only"
  type        = string
  sensitive   = true
  default     = ""
}

variable "nextauth_secret" {
  description = "Initial NextAuth.js JWT secret — seeded into SSM at /mediaflows/<env>/nextauth-secret on first apply only"
  type        = string
  sensitive   = true
  default     = ""
}

variable "github_access_token" {
  description = "GitHub PAT with `repo` scope for Amplify — seeded into SSM at /mediaflows/<env>/github-pat on first apply only"
  type        = string
  sensitive   = true
  default     = ""
}
