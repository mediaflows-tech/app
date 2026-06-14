# infrastructure/modules/database/variables.tf
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
  description = "VPC ID for the DB subnet group"
  type        = string
}

variable "db_subnet_ids" {
  description = "Subnet IDs for the DB subnet group"
  type        = list(string)
}

variable "db_security_group_id" {
  description = "Security group ID for RDS"
  type        = string
}

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
  sensitive   = true
}

variable "enabled" {
  description = "When false, the RDS instance is not created. Other DB module resources (subnet group, DynamoDB tables) stay up — those are free or store data we want preserved."
  type        = bool
  default     = true
}

variable "restore_snapshot_id" {
  description = "If non-empty, restore the RDS instance from this snapshot ID at create time. No-op on existing instances."
  type        = string
  default     = ""
}
