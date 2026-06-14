# infrastructure/modules/dns/outputs.tf
output "zone_id" {
  description = "Route53 hosted zone ID"
  value       = aws_route53_zone.main.zone_id
}

output "name_servers" {
  description = "Route53 name servers — update these at your registrar"
  value       = aws_route53_zone.main.name_servers
}

output "acm_certificate_arn_regional" {
  description = "ACM certificate ARN in ap-southeast-1 (for ALB)"
  value       = aws_acm_certificate_validation.regional.certificate_arn
}

output "acm_certificate_arn_cloudfront" {
  description = "ACM certificate ARN in us-east-1 (for CloudFront + Cognito)"
  value       = aws_acm_certificate_validation.cloudfront.certificate_arn
}

output "domain_name" {
  description = "Root domain name"
  value       = var.domain_name
}
