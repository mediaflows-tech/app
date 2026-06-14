# infrastructure/outputs.tf

output "vpc_id" {
  description = "VPC ID"
  value       = module.networking.vpc_id
}

output "rds_endpoint" {
  description = "RDS PostgreSQL endpoint (empty when stopped)"
  value       = try(module.database.rds_endpoint, "")
}

output "s3_bucket_name" {
  description = "Media assets S3 bucket name"
  value       = module.storage.bucket_name
}

output "cloudfront_domain" {
  description = "CloudFront distribution domain name"
  value       = module.cdn.cloudfront_domain_name
}

output "cognito_user_pool_id" {
  description = "Cognito User Pool ID"
  value       = module.auth.user_pool_id
}

output "cognito_client_id" {
  description = "Cognito App Client ID"
  value       = module.auth.client_id
}

output "api_gateway_url" {
  description = "API Gateway invoke URL"
  value       = module.serverless.api_gateway_invoke_url
}

output "eb_environment_url" {
  description = "Elastic Beanstalk environment URL (empty when stopped)"
  value       = try(module.compute.eb_environment_url, "")
}

output "domain_name" {
  description = "Custom domain name"
  value       = var.domain_name != "" ? var.domain_name : null
}

output "name_servers" {
  description = "Route53 name servers — update these at your registrar"
  value       = var.domain_name != "" ? module.dns[0].name_servers : null
}

output "amplify_app_id" {
  description = "Amplify frontend app ID"
  value       = module.amplify.app_id
}

output "amplify_default_domain" {
  description = "Amplify default domain (before custom domain setup)"
  value       = module.amplify.default_domain
}
