# infrastructure/modules/amplify/outputs.tf
output "app_id" {
  description = "Amplify app ID"
  value       = aws_amplify_app.frontend.id
}

output "app_arn" {
  description = "Amplify app ARN"
  value       = aws_amplify_app.frontend.arn
}

output "default_domain" {
  description = "Amplify default domain"
  value       = aws_amplify_app.frontend.default_domain
}

output "branch_name" {
  description = "Deployed branch name"
  value       = aws_amplify_branch.main.branch_name
}

output "custom_domain_verification_dns" {
  description = "DNS records for custom domain verification (if configured)"
  value       = length(aws_amplify_domain_association.main) > 0 ? aws_amplify_domain_association.main[0].certificate_verification_dns_record : null
}
