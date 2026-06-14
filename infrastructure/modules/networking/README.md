# `networking` Module

Provisions the VPC, subnets, NAT Gateway, route tables, S3 VPC endpoint, and security groups that form the network foundation for all other MediaFlows modules.

> Top-level project README is at [`../../../README.md`](../../../README.md).

## Resources

- `aws_vpc.main` — VPC with DNS support and hostnames enabled
- `aws_internet_gateway.main` — Internet gateway for public subnets
- `aws_subnet.public[0..1]` — Two public subnets (ALB, NAT Gateway), across two AZs
- `aws_subnet.private_app[0..1]` — Two private app subnets (Elastic Beanstalk EC2 instances)
- `aws_subnet.private_db[0..1]` — Two private DB subnets (RDS PostgreSQL)
- `aws_eip.nat` + `aws_nat_gateway.main` — Single NAT Gateway (cost-optimised; not HA)
- `aws_route_table.public` + `aws_route_table.private` — Route tables with appropriate default routes
- `aws_route_table_association.*` — Associates subnets to their route tables
- `aws_vpc_endpoint.s3` — Free S3 Gateway endpoint to avoid NAT costs for S3 traffic
- `aws_security_group.alb` — Allows inbound HTTPS/HTTP from anywhere; all outbound
- `aws_security_group.app` — Allows inbound on port 5000 from ALB SG only; all outbound
- `aws_security_group.db` — Allows PostgreSQL (5432) from app and Lambda SGs; all outbound
- `aws_security_group.lambda` — Outbound-only (Lambda → RDS, SNS, SQS via NAT/endpoints)
- `aws_security_group_rule.db_from_lambda` — Ingress rule: Lambda SG → DB SG on port 5432

## Inputs

| Variable             | Type           | Description                                                         |
| -------------------- | -------------- | ------------------------------------------------------------------- |
| `environment`        | `string`       | Deployment environment                                              |
| `project_name`       | `string`       | Project name prefix (default: `mediaflows`)                         |
| `vpc_cidr`           | `string`       | VPC CIDR block (default: `10.0.0.0/16`)                             |
| `availability_zones` | `list(string)` | AZs for subnets (default: `["ap-southeast-1a", "ap-southeast-1b"]`) |

## Outputs

| Output              | Description              |
| ------------------- | ------------------------ |
| `vpc_id`            | VPC ID                   |
| `public_subnet_ids` | Public subnet IDs        |
| `app_subnet_ids`    | Private app subnet IDs   |
| `db_subnet_ids`     | Private DB subnet IDs    |
| `alb_sg_id`         | ALB security group ID    |
| `app_sg_id`         | App security group ID    |
| `db_sg_id`          | DB security group ID     |
| `lambda_sg_id`      | Lambda security group ID |

## Notes

- A single NAT Gateway is used to minimise cost. For production HA, add a second NAT Gateway in the second AZ and add a separate private route table per AZ.
- CIDR layout: public `10.0.1-2.0/24`, private app `10.0.10-11.0/24`, private DB `10.0.20-21.0/24`.
