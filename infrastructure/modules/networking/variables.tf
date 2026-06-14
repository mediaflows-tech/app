# infrastructure/modules/networking/variables.tf
variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "mediaflows"
}

variable "vpc_cidr" {
  description = "VPC CIDR block"
  type        = string
  default     = "10.0.0.0/16"
}

variable "availability_zones" {
  description = "Availability zones for subnets"
  type        = list(string)
  default     = ["ap-southeast-1a", "ap-southeast-1b"]
}

variable "nat_enabled" {
  description = "When false, the NAT Gateway, its EIP, and the 0.0.0.0/0 NAT route are not created. VPC, subnets, IGW, public route table, and route table associations stay up (free)."
  type        = bool
  default     = true
}
