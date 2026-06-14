# infrastructure/modules/monitoring/outputs.tf
output "dashboard_name" {
  description = "CloudWatch dashboard name (empty when EB stopped)"
  value       = try(aws_cloudwatch_dashboard.main[0].dashboard_name, "")
}

output "high_cpu_alarm_arn" {
  description = "High CPU alarm ARN (empty when EB stopped)"
  value       = try(aws_cloudwatch_metric_alarm.high_cpu[0].arn, "")
}

output "billing_alarm_arn" {
  description = "Billing alarm ARN"
  value       = aws_cloudwatch_metric_alarm.billing.arn
}

output "xray_group_name" {
  description = "X-Ray group name"
  value       = aws_xray_group.main.group_name
}
