# infrastructure/bootstrap/providers.tf
# Local-state stack — intentionally no backend {} block.
# The outputs of this stack configure the S3 backend used by the parent
# infrastructure/ stack.
terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.80"
    }
    tls = {
      source  = "hashicorp/tls"
      version = "~> 4.0"
    }
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Environment = var.environment
      Project     = "MediaFlows"
      Module      = "CT071-3-3-DDAC"
      ManagedBy   = "Terraform"
      Stack       = "bootstrap"
    }
  }
}
