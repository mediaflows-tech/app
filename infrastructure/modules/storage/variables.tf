# infrastructure/modules/storage/variables.tf
variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "mediaflows"
}

variable "cors_allowed_origins" {
  description = "Allowed origins for S3 CORS (presigned URL uploads)"
  type        = list(string)
  default     = ["http://localhost:5000", "https://localhost:5001"]
}
