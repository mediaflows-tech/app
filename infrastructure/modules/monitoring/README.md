# `monitoring` Module

Provisions CloudWatch alarms, a CloudWatch dashboard, and X-Ray sampling rules to observe the MediaFlows platform's health and cost.

> Top-level project README is at [`../../../README.md`](../../../README.md).

## Resources

- `aws_cloudwatch_metric_alarm.high_cpu` — Alarm: EC2 CPU > 80% for 3 of 5 minutes
- `aws_cloudwatch_metric_alarm.high_5xx` — Alarm: ALB 5XX errors > 10 in 5 minutes
- `aws_cloudwatch_metric_alarm.high_latency` — Alarm: P95 response time > 2s in 5 minutes
- `aws_cloudwatch_metric_alarm.queue_depth` — Alarm: SQS media-processing queue depth > 1000 messages
- `aws_cloudwatch_metric_alarm.lambda_errors` — One alarm per Lambda function: errors > 5 in 5 minutes (`for_each` over `lambda_function_names`)
- `aws_cloudwatch_metric_alarm.post_confirm_group_assign_failures` — Alarm: PostConfirmationGroupAssigner swallowed ≥ 1 Cognito group-add failure in 5 minutes (EMF metric `MediaFlows/PostConfirmation/GroupAssignFailures`)
- `aws_cloudwatch_metric_alarm.billing` — Alarm: estimated AWS charges > $5 (checked every 6 hours)
- `aws_cloudwatch_dashboard.main` — Dashboard with CPU, ALB latency/requests/errors, SQS depth, Lambda invocations and duration
- `aws_xray_group.main` — X-Ray group filtering on `service("MediaFlows")`
- `aws_xray_sampling_rule.main` — 5% sampling rate across all MediaFlows requests

## Inputs

| Variable                      | Type           | Description                                                                            |
| ----------------------------- | -------------- | -------------------------------------------------------------------------------------- |
| `environment`                 | `string`       | Deployment environment                                                                 |
| `project_name`                | `string`       | Project name prefix (default: `mediaflows`)                                            |
| `system_alerts_topic_arn`     | `string`       | SNS topic ARN for alarm action notifications                                           |
| `eb_environment_name`         | `string`       | EB environment name used for CPU and dashboard metrics                                 |
| `media_processing_queue_name` | `string`       | SQS queue name for queue-depth alarm (default: `""`)                                   |
| `lambda_function_names`       | `list(string)` | Lambda function names to create error alarms and dashboard widgets for (default: `[]`) |

## Outputs

| Output               | Description               |
| -------------------- | ------------------------- |
| `dashboard_name`     | CloudWatch dashboard name |
| `high_cpu_alarm_arn` | High CPU alarm ARN        |
| `billing_alarm_arn`  | Billing alarm ARN         |
| `xray_group_name`    | X-Ray group name          |

## Notes

- The billing alarm uses the `AWS/Billing` namespace which is only populated in `us-east-1`. The module is applied in `ap-southeast-1` but the metric is global — the alarm still triggers correctly.
- All alarms notify (and clear to) the `system_alerts_topic_arn`. Subscribe an email address to that SNS topic for operational alerting.
