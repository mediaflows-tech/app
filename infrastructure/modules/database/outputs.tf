# infrastructure/modules/database/outputs.tf
output "rds_endpoint" {
  description = "RDS PostgreSQL endpoint (empty when stopped)"
  value       = try(aws_db_instance.postgresql[0].endpoint, "")
}

output "rds_address" {
  description = "RDS PostgreSQL address (hostname only, empty when stopped)"
  value       = try(aws_db_instance.postgresql[0].address, "")
}

output "rds_port" {
  description = "RDS PostgreSQL port (0 when stopped)"
  value       = try(aws_db_instance.postgresql[0].port, 0)
}

output "rds_db_name" {
  description = "RDS database name (empty when stopped)"
  value       = try(aws_db_instance.postgresql[0].db_name, "")
}

output "connection_string" {
  description = "PostgreSQL connection string for application (empty when stopped). Consumers will fail at runtime if they try to use this — acceptable because no traffic reaches them when stopped."
  value       = try("Host=${aws_db_instance.postgresql[0].address};Port=${aws_db_instance.postgresql[0].port};Database=${aws_db_instance.postgresql[0].db_name};Username=${var.db_username};Password=${data.aws_ssm_parameter.db_password.value}", "")
  sensitive   = true
}

output "view_counters_table_name" {
  description = "DynamoDB ViewCounters table name"
  value       = aws_dynamodb_table.view_counters.name
}

output "trending_data_table_name" {
  description = "DynamoDB TrendingData table name"
  value       = aws_dynamodb_table.trending_data.name
}

output "activity_feed_table_name" {
  description = "DynamoDB ActivityFeed table name"
  value       = aws_dynamodb_table.activity_feed.name
}

output "sessions_table_name" {
  description = "DynamoDB Sessions table name"
  value       = aws_dynamodb_table.sessions.name
}
