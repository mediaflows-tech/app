# infrastructure/modules/auth/outputs.tf
output "user_pool_id" {
  description = "Cognito User Pool ID"
  value       = aws_cognito_user_pool.main.id
}

output "user_pool_arn" {
  description = "Cognito User Pool ARN"
  value       = aws_cognito_user_pool.main.arn
}

output "client_id" {
  description = "Cognito App Client ID"
  value       = aws_cognito_user_pool_client.web_app.id
}

output "client_secret" {
  description = "Cognito App Client Secret"
  value       = aws_cognito_user_pool_client.web_app.client_secret
  sensitive   = true
}

output "user_pool_domain" {
  description = "Cognito User Pool domain"
  value       = var.custom_domain != "" ? aws_cognito_user_pool_domain.custom[0].domain : aws_cognito_user_pool_domain.prefix[0].domain
}

output "domain_url" {
  description = "Cognito hosted UI domain URL"
  value       = var.custom_domain != "" ? "https://${var.custom_domain}" : "https://${aws_cognito_user_pool_domain.prefix[0].domain}.auth.ap-southeast-1.amazoncognito.com"
}

output "authority" {
  description = "OIDC authority URL"
  value       = "https://cognito-idp.ap-southeast-1.amazonaws.com/${aws_cognito_user_pool.main.id}"
}

output "issuer" {
  description = "OIDC issuer URL (same as authority)"
  value       = "https://cognito-idp.ap-southeast-1.amazonaws.com/${aws_cognito_user_pool.main.id}"
}

output "cognito_custom_domain_cloudfront" {
  description = "CloudFront distribution domain for Cognito custom domain (empty if prefix domain)"
  value       = var.custom_domain != "" ? aws_cognito_user_pool_domain.custom[0].cloudfront_distribution : ""
}

output "cognito_custom_domain_cloudfront_zone_id" {
  description = "CloudFront hosted zone ID for Cognito custom domain (empty if prefix domain)"
  value       = var.custom_domain != "" ? aws_cognito_user_pool_domain.custom[0].cloudfront_distribution_zone_id : ""
}
