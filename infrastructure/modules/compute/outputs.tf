# infrastructure/modules/compute/outputs.tf
output "eb_application_name" {
  description = "Elastic Beanstalk application name (empty when stopped)"
  value       = try(aws_elastic_beanstalk_application.main[0].name, "")
}

output "eb_environment_name" {
  description = "Elastic Beanstalk environment name (empty when stopped)"
  value       = try(aws_elastic_beanstalk_environment.main[0].name, "")
}

output "eb_environment_url" {
  description = "Elastic Beanstalk environment URL (empty when stopped)"
  value       = try(aws_elastic_beanstalk_environment.main[0].endpoint_url, "")
}

output "eb_environment_cname" {
  description = "Elastic Beanstalk environment CNAME (empty when stopped)"
  value       = try(aws_elastic_beanstalk_environment.main[0].cname, "")
}

output "eb_instance_profile_arn" {
  description = "EB EC2 instance profile ARN (kept across stop/start)"
  value       = aws_iam_instance_profile.eb_ec2_profile.arn
}

output "alb_dns_name" {
  description = "ALB DNS name for Route53 alias (empty when stopped)"
  value       = try(data.aws_lb.eb_alb[0].dns_name, "")
}

output "alb_zone_id" {
  description = "ALB hosted zone ID for Route53 alias (empty when stopped)"
  value       = try(data.aws_lb.eb_alb[0].zone_id, "")
}
