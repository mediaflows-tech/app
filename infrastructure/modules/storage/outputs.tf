# infrastructure/modules/storage/outputs.tf
output "bucket_name" {
  description = "S3 bucket name for media assets"
  value       = aws_s3_bucket.media_assets.bucket
}

output "bucket_arn" {
  description = "S3 bucket ARN"
  value       = aws_s3_bucket.media_assets.arn
}

output "bucket_id" {
  description = "S3 bucket ID"
  value       = aws_s3_bucket.media_assets.id
}

output "bucket_regional_domain_name" {
  description = "S3 bucket regional domain name (for CloudFront origin)"
  value       = aws_s3_bucket.media_assets.bucket_regional_domain_name
}
