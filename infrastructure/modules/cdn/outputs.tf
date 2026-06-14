# infrastructure/modules/cdn/outputs.tf
output "cloudfront_distribution_id" {
  description = "CloudFront distribution ID"
  value       = aws_cloudfront_distribution.media.id
}

output "cloudfront_domain_name" {
  description = "CloudFront distribution domain name"
  value       = aws_cloudfront_distribution.media.domain_name
}

output "cloudfront_arn" {
  description = "CloudFront distribution ARN"
  value       = aws_cloudfront_distribution.media.arn
}

output "cloudfront_hosted_zone_id" {
  description = "CloudFront distribution hosted zone ID for Route53 alias"
  value       = aws_cloudfront_distribution.media.hosted_zone_id
}
