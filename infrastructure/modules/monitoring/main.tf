# infrastructure/modules/monitoring/main.tf

# ──────────────────────────────────────────────────
# CloudWatch Alarms
# ──────────────────────────────────────────────────

# CPU Utilization > 80% for 5 minutes (3 of 5 datapoints).
# Gated on eb_enabled so the alarm is destroyed when services are stopped;
# otherwise the EnvironmentName dimension would be empty (and CloudWatch
# rejects PutMetricAlarm with empty dimension values).
resource "aws_cloudwatch_metric_alarm" "high_cpu" {
  count               = var.eb_enabled ? 1 : 0
  alarm_name          = "${var.project_name}-high-cpu-${var.environment}"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 5
  datapoints_to_alarm = 3
  metric_name         = "CPUUtilization"
  namespace           = "AWS/EC2"
  period              = 60
  statistic           = "Average"
  threshold           = 80
  alarm_description   = "CPU utilization exceeds 80% for 3 of 5 minutes"
  alarm_actions       = [var.system_alerts_topic_arn]
  ok_actions          = [var.system_alerts_topic_arn]

  dimensions = {
    EnvironmentName = var.eb_environment_name
  }

  tags = {
    Name = "${var.project_name}-high-cpu-${var.environment}"
  }
}

# 5XX Error Count > 10 in 5 minutes (3 of 5 datapoints)
resource "aws_cloudwatch_metric_alarm" "high_5xx" {
  alarm_name          = "${var.project_name}-high-5xx-${var.environment}"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 5
  datapoints_to_alarm = 3
  metric_name         = "HTTPCode_Target_5XX_Count"
  namespace           = "AWS/ApplicationELB"
  period              = 60
  statistic           = "Sum"
  threshold           = 10
  alarm_description   = "5XX errors exceed 10 in 5 minutes"
  alarm_actions       = [var.system_alerts_topic_arn]
  ok_actions          = [var.system_alerts_topic_arn]
  treat_missing_data  = "notBreaching"

  tags = {
    Name = "${var.project_name}-high-5xx-${var.environment}"
  }
}

# P95 Response Time > 2 seconds (3 of 5 datapoints)
resource "aws_cloudwatch_metric_alarm" "high_latency" {
  alarm_name          = "${var.project_name}-high-latency-${var.environment}"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 5
  datapoints_to_alarm = 3
  metric_name         = "TargetResponseTime"
  namespace           = "AWS/ApplicationELB"
  period              = 60
  extended_statistic  = "p95"
  threshold           = 2
  alarm_description   = "P95 response time exceeds 2 seconds"
  alarm_actions       = [var.system_alerts_topic_arn]
  treat_missing_data  = "notBreaching"

  tags = {
    Name = "${var.project_name}-high-latency-${var.environment}"
  }
}

# SQS Queue Depth > 1000 messages (1 of 1 datapoints)
resource "aws_cloudwatch_metric_alarm" "queue_depth" {
  alarm_name          = "${var.project_name}-queue-depth-${var.environment}"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  datapoints_to_alarm = 1
  metric_name         = "ApproximateNumberOfMessagesVisible"
  namespace           = "AWS/SQS"
  period              = 300
  statistic           = "Maximum"
  threshold           = 1000
  alarm_description   = "SQS queue depth exceeds 1000 messages"
  alarm_actions       = [var.system_alerts_topic_arn]

  dimensions = {
    QueueName = var.media_processing_queue_name
  }

  tags = {
    Name = "${var.project_name}-queue-depth-${var.environment}"
  }
}

# Lambda Errors > 5 in 5 minutes (3 of 5 datapoints) — one alarm per function
resource "aws_cloudwatch_metric_alarm" "lambda_errors" {
  for_each = toset(var.lambda_function_names)

  alarm_name          = "${var.project_name}-lambda-errors-${each.value}-${var.environment}"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 5
  datapoints_to_alarm = 3
  metric_name         = "Errors"
  namespace           = "AWS/Lambda"
  period              = 60
  statistic           = "Sum"
  threshold           = 5
  alarm_description   = "Lambda function ${each.value} errors exceed 5 in 5 minutes"
  alarm_actions       = [var.system_alerts_topic_arn]
  treat_missing_data  = "notBreaching"

  dimensions = {
    FunctionName = each.value
  }

  tags = {
    Name = "${var.project_name}-lambda-errors-${each.value}-${var.environment}"
  }
}

# PostConfirmationGroupAssigner swallows Cognito group-add failures by design
# (Cognito post-confirmation is blocking — failing the user is worse than
# missing the group assignment). The Lambda emits an EMF metric on the
# swallowed branch so this alarm can surface the failure for admin
# remediation. Volume is naturally low; one occurrence deserves attention.
resource "aws_cloudwatch_metric_alarm" "post_confirm_group_assign_failures" {
  alarm_name          = "${var.project_name}-post-confirm-group-assign-failures-${var.environment}"
  comparison_operator = "GreaterThanOrEqualToThreshold"
  evaluation_periods  = 1
  datapoints_to_alarm = 1
  metric_name         = "GroupAssignFailures"
  namespace           = "MediaFlows/PostConfirmation"
  period              = 300
  statistic           = "Sum"
  threshold           = 1
  alarm_description   = "PostConfirmationGroupAssigner swallowed a Cognito group-add failure — user signed up but was not added to the default group. Manual admin remediation may be required."
  alarm_actions       = [var.system_alerts_topic_arn]
  treat_missing_data  = "notBreaching"

  tags = {
    Name = "${var.project_name}-post-confirm-group-assign-failures-${var.environment}"
  }
}

# Billing Alarm > $5 (1 of 1 datapoints)
resource "aws_cloudwatch_metric_alarm" "billing" {
  alarm_name          = "${var.project_name}-billing-${var.environment}"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  datapoints_to_alarm = 1
  metric_name         = "EstimatedCharges"
  namespace           = "AWS/Billing"
  period              = 21600 # 6 hours
  statistic           = "Maximum"
  threshold           = 5
  alarm_description   = "Estimated AWS charges exceed $5"
  alarm_actions       = [var.system_alerts_topic_arn]

  dimensions = {
    Currency = "USD"
  }

  tags = {
    Name = "${var.project_name}-billing-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# CloudWatch Dashboard
# ──────────────────────────────────────────────────
# Gated on eb_enabled — the dashboard body embeds the EB env name in
# metric definitions; PutDashboard rejects empty strings.
resource "aws_cloudwatch_dashboard" "main" {
  count          = var.eb_enabled ? 1 : 0
  dashboard_name = "${var.project_name}-${var.environment}"

  dashboard_body = jsonencode({
    widgets = [
      {
        type   = "metric"
        x      = 0
        y      = 0
        width  = 12
        height = 6
        properties = {
          title = "EC2 CPU Utilization"
          metrics = [
            ["AWS/EC2", "CPUUtilization", "EnvironmentName", var.eb_environment_name]
          ]
          period = 300
          stat   = "Average"
          region = "ap-southeast-1"
          view   = "timeSeries"
        }
      },
      {
        type   = "metric"
        x      = 12
        y      = 0
        width  = 12
        height = 6
        properties = {
          title = "ALB Response Time (P50/P95/P99)"
          metrics = [
            ["AWS/ApplicationELB", "TargetResponseTime", { stat = "p50", label = "P50" }],
            ["AWS/ApplicationELB", "TargetResponseTime", { stat = "p95", label = "P95" }],
            ["AWS/ApplicationELB", "TargetResponseTime", { stat = "p99", label = "P99" }]
          ]
          period = 300
          region = "ap-southeast-1"
          view   = "timeSeries"
        }
      },
      {
        type   = "metric"
        x      = 0
        y      = 6
        width  = 12
        height = 6
        properties = {
          title = "ALB Request Count & 5XX Errors"
          metrics = [
            ["AWS/ApplicationELB", "RequestCount", { stat = "Sum", label = "Requests" }],
            ["AWS/ApplicationELB", "HTTPCode_Target_5XX_Count", { stat = "Sum", label = "5XX Errors" }]
          ]
          period = 300
          region = "ap-southeast-1"
          view   = "timeSeries"
        }
      },
      {
        type   = "metric"
        x      = 12
        y      = 6
        width  = 12
        height = 6
        properties = {
          title = "SQS Queue Depth"
          metrics = [
            ["AWS/SQS", "ApproximateNumberOfMessagesVisible", "QueueName", var.media_processing_queue_name]
          ]
          period = 300
          stat   = "Maximum"
          region = "ap-southeast-1"
          view   = "timeSeries"
        }
      },
      {
        type   = "metric"
        x      = 0
        y      = 12
        width  = 24
        height = 6
        properties = {
          title = "Lambda Invocations & Errors"
          metrics = [
            for fn in var.lambda_function_names : [
              "AWS/Lambda", "Invocations", "FunctionName", fn, { stat = "Sum" }
            ]
          ]
          period = 300
          region = "ap-southeast-1"
          view   = "timeSeries"
        }
      },
      {
        type   = "metric"
        x      = 0
        y      = 18
        width  = 24
        height = 6
        properties = {
          title = "Lambda Duration (Avg)"
          metrics = [
            for fn in var.lambda_function_names : [
              "AWS/Lambda", "Duration", "FunctionName", fn, { stat = "Average" }
            ]
          ]
          period = 300
          region = "ap-southeast-1"
          view   = "timeSeries"
        }
      }
    ]
  })
}

# ──────────────────────────────────────────────────
# X-Ray Sampling Rule and Group
# ──────────────────────────────────────────────────
resource "aws_xray_group" "main" {
  group_name        = "${var.project_name}-${var.environment}"
  filter_expression = "service(\"MediaFlows\")"

  tags = {
    Name = "${var.project_name}-xray-group-${var.environment}"
  }
}

resource "aws_xray_sampling_rule" "main" {
  rule_name      = "${var.project_name}-${var.environment}"
  priority       = 1000
  version        = 1
  reservoir_size = 1
  fixed_rate     = 0.05 # Sample 5% of requests
  url_path       = "*"
  host           = "*"
  http_method    = "*"
  service_type   = "*"
  service_name   = "MediaFlows"
  resource_arn   = "*"

  tags = {
    Name = "${var.project_name}-xray-sampling-${var.environment}"
  }
}
