# infrastructure/modules/database/main.tf

data "aws_ssm_parameter" "db_password" {
  name = "/${var.project_name}/${var.environment}/db-password"
}

# ──────────────────────────────────────────────────
# RDS PostgreSQL
# ──────────────────────────────────────────────────
resource "aws_db_subnet_group" "main" {
  name       = "${var.project_name}-db-subnet-${var.environment}"
  subnet_ids = var.db_subnet_ids

  tags = {
    Name = "${var.project_name}-db-subnet-group-${var.environment}"
  }
}

# Gated on var.enabled via count. Migrate existing state from the singleton
# address to the count[0] address so Terraform treats this as an in-place
# update rather than a destroy/recreate.
moved {
  from = aws_db_instance.postgresql
  to   = aws_db_instance.postgresql[0]
}

resource "aws_db_instance" "postgresql" {
  count = var.enabled ? 1 : 0

  identifier     = "${var.project_name}-db-${var.environment}"
  engine         = "postgres"
  engine_version = "16.6"
  instance_class = var.db_instance_class

  allocated_storage     = var.db_allocated_storage
  max_allocated_storage = 100 # Enable autoscaling up to 100GB
  storage_type          = "gp3"
  storage_encrypted     = true

  db_name  = "mediaflows"
  username = var.db_username
  password = data.aws_ssm_parameter.db_password.value

  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [var.db_security_group_id]

  multi_az            = false # Single-AZ for Free Tier
  publicly_accessible = false

  backup_retention_period = 1 # Free Tier limit
  backup_window           = "03:00-04:00"
  maintenance_window      = "sun:04:00-sun:05:00"

  # Snapshots are managed out-of-band (a named snapshot is taken before any
  # gated teardown), so skip_final_snapshot avoids Terraform racing that
  # snapshot.
  skip_final_snapshot = true
  deletion_protection = false

  # Honored only at create. Empty string means "fresh DB"; a snapshot ID
  # restores from that snapshot.
  snapshot_identifier = var.restore_snapshot_id != "" ? var.restore_snapshot_id : null

  performance_insights_enabled = false

  tags = {
    Name = "${var.project_name}-postgresql-${var.environment}"
  }

  lifecycle {
    prevent_destroy = false
  }
}

# ──────────────────────────────────────────────────
# DynamoDB Tables
# ──────────────────────────────────────────────────

# Table names use project prefix only (no env suffix) to match
# the DynamoDBContext TableNamePrefix + [DynamoDBTable] attribute pattern
resource "aws_dynamodb_table" "view_counters" {
  name         = "${var.project_name}-ViewCounters"
  billing_mode = "PAY_PER_REQUEST" # Free Tier covers 25 RCU/WCU equivalent
  hash_key     = "AssetId"

  attribute {
    name = "AssetId"
    type = "S"
  }

  tags = {
    Name = "${var.project_name}-ViewCounters"
  }
}

resource "aws_dynamodb_table" "trending_data" {
  name         = "${var.project_name}-TrendingData"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "TimeBucket"
  range_key    = "ScoreAssetId"

  attribute {
    name = "TimeBucket"
    type = "S"
  }

  attribute {
    name = "ScoreAssetId"
    type = "S"
  }

  ttl {
    attribute_name = "ExpiresAt"
    enabled        = true
  }

  tags = {
    Name = "${var.project_name}-TrendingData"
  }
}

resource "aws_dynamodb_table" "activity_feed" {
  name         = "${var.project_name}-ActivityFeed"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "UserId"
  range_key    = "Timestamp"

  attribute {
    name = "UserId"
    type = "S"
  }

  attribute {
    name = "Timestamp"
    type = "S"
  }

  ttl {
    attribute_name = "ExpiresAt"
    enabled        = true
  }

  tags = {
    Name = "${var.project_name}-ActivityFeed"
  }
}

resource "aws_dynamodb_table" "sessions" {
  name         = "${var.project_name}-Sessions"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "SessionId"

  attribute {
    name = "SessionId"
    type = "S"
  }

  ttl {
    attribute_name = "ExpiresAt"
    enabled        = true
  }

  tags = {
    Name = "${var.project_name}-Sessions"
  }
}
