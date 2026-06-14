# `database` Module

Provisions the RDS PostgreSQL instance (primary relational store) and four DynamoDB tables used for high-throughput counters, trending data, activity feeds, and session state.

> Top-level project README is at [`../../../README.md`](../../../README.md).

## Resources

- `aws_db_subnet_group.main` — DB subnet group spanning the private DB subnets
- `aws_db_instance.postgresql` — PostgreSQL 16.6 on `db.t3.micro`, encrypted, with automated backups and autoscaling up to 100 GB
- `aws_dynamodb_table.view_counters` — `mediaflows-ViewCounters` — per-asset view count (hash key: `AssetId`)
- `aws_dynamodb_table.trending_data` — `mediaflows-TrendingData` — daily trending leaderboard with TTL (hash: `TimeBucket`, range: `ScoreAssetId`)
- `aws_dynamodb_table.activity_feed` — `mediaflows-ActivityFeed` — per-user activity timeline with TTL (hash: `UserId`, range: `Timestamp`)
- `aws_dynamodb_table.sessions` — `mediaflows-Sessions` — distributed session store with TTL (hash: `SessionId`)

## Inputs

| Variable               | Type           | Description                                     |
| ---------------------- | -------------- | ----------------------------------------------- |
| `environment`          | `string`       | Deployment environment                          |
| `project_name`         | `string`       | Project name prefix (default: `mediaflows`)     |
| `vpc_id`               | `string`       | VPC ID (used for subnet group)                  |
| `db_subnet_ids`        | `list(string)` | Private DB subnet IDs                           |
| `db_security_group_id` | `string`       | Security group for RDS                          |
| `db_instance_class`    | `string`       | RDS instance class (default: `db.t3.micro`)     |
| `db_allocated_storage` | `number`       | Initial allocated storage in GB (default: `20`) |
| `db_username`          | `string`       | RDS master username (sensitive)                 |

## Outputs

| Output                     | Description                                   |
| -------------------------- | --------------------------------------------- |
| `rds_endpoint`             | RDS endpoint (`host:port`)                    |
| `rds_address`              | RDS hostname                                  |
| `rds_port`                 | RDS port                                      |
| `rds_db_name`              | Database name (`mediaflows`)                  |
| `connection_string`        | Full PostgreSQL connection string (sensitive) |
| `view_counters_table_name` | DynamoDB ViewCounters table name              |
| `trending_data_table_name` | DynamoDB TrendingData table name              |
| `activity_feed_table_name` | DynamoDB ActivityFeed table name              |
| `sessions_table_name`      | DynamoDB Sessions table name                  |

## Notes

- The DB password is read from SSM Parameter Store at `/<project>/<env>/db-password` — it must be created manually before the first `terraform apply`.
- DynamoDB table names use `<project>-<TableName>` without an environment suffix, matching the `DynamoDBContext` `TableNamePrefix` + `[DynamoDBTable]` attribute pattern used in the .NET SDK.
- `multi_az` is disabled and `backup_retention_period` is 1 day to stay within Free Tier limits.
