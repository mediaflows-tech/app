# infrastructure/modules/serverless/main.tf

# ──────────────────────────────────────────────────
# IAM Role for Lambda Execution
# ──────────────────────────────────────────────────
resource "aws_iam_role" "lambda_execution" {
  name = "${var.project_name}-lambda-exec-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "lambda_basic" {
  role       = aws_iam_role.lambda_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_iam_role_policy_attachment" "lambda_vpc" {
  role       = aws_iam_role.lambda_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

resource "aws_iam_role_policy_attachment" "lambda_xray" {
  role       = aws_iam_role.lambda_execution.name
  policy_arn = "arn:aws:iam::aws:policy/AWSXRayDaemonWriteAccess"
}

resource "aws_iam_role_policy" "lambda_app_policy" {
  name = "${var.project_name}-lambda-app-policy-${var.environment}"
  role = aws_iam_role.lambda_execution.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "S3Access"
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:PutObject",
          "s3:DeleteObject",
          "s3:CopyObject",
          "s3:GetObjectMetadata"
        ]
        Resource = [
          "${var.s3_bucket_arn}/*"
        ]
      },
      {
        Sid    = "DynamoDBAccess"
        Effect = "Allow"
        Action = [
          "dynamodb:GetItem",
          "dynamodb:PutItem",
          "dynamodb:UpdateItem",
          "dynamodb:DeleteItem",
          "dynamodb:Query",
          "dynamodb:Scan",
          "dynamodb:BatchGetItem",
          "dynamodb:BatchWriteItem"
        ]
        Resource = "arn:aws:dynamodb:*:*:table/${var.project_name}-*"
      },
      {
        Sid    = "RekognitionAccess"
        Effect = "Allow"
        Action = [
          "rekognition:DetectLabels",
          "rekognition:DetectModerationLabels"
        ]
        Resource = "*"
      },
      {
        Sid      = "SNSPublish"
        Effect   = "Allow"
        Action   = "sns:Publish"
        Resource = "arn:aws:sns:*:*:${var.project_name}-*"
      },
      {
        Sid    = "SQSAccess"
        Effect = "Allow"
        Action = [
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:GetQueueAttributes"
        ]
        Resource = "arn:aws:sqs:*:*:${var.project_name}-*"
      },
      {
        Sid    = "CloudWatchMetrics"
        Effect = "Allow"
        Action = [
          "cloudwatch:PutMetricData"
        ]
        Resource = "*"
      }
      # NOTE: The Cognito AdminAddUserToGroup permission required by the
      # PostConfirmationGroupAssigner Lambda lives at the root module level
      # (aws_iam_role_policy "lambda_cognito_admin" in infrastructure/main.tf)
      # because it needs to reference the auth module's user pool ARN, and
      # placing that reference here would create a module-level dependency
      # cycle between the auth and serverless modules.
    ]
  })
}

# ──────────────────────────────────────────────────
# Lambda Functions (placeholder — zip deployed via CI/CD)
# ──────────────────────────────────────────────────

# Placeholder zip for initial terraform apply (before first CI/CD deploy)
data "archive_file" "lambda_placeholder" {
  type        = "zip"
  output_path = "${path.module}/placeholder.zip"

  source {
    content  = "placeholder"
    filename = "placeholder.txt"
  }
}

# 1. ThumbnailGenerator — SQS-triggered
resource "aws_lambda_function" "thumbnail_generator" {
  function_name = "${var.project_name}-ThumbnailGenerator-${var.environment}"
  role          = aws_iam_role.lambda_execution.arn
  handler       = "MediaFlows.Lambda.ThumbnailGenerator::MediaFlows.Lambda.ThumbnailGenerator.Function::FunctionHandler"
  runtime       = "dotnet8"
  architectures = ["arm64"]
  memory_size   = var.lambda_memory_size
  timeout       = var.lambda_timeout

  filename         = data.archive_file.lambda_placeholder.output_path
  source_code_hash = data.archive_file.lambda_placeholder.output_base64sha256

  environment {
    variables = {
      OUTPUT_BUCKET = var.s3_bucket_name
    }
  }

  tracing_config {
    mode = "Active"
  }

  tags = {
    Name     = "${var.project_name}-ThumbnailGenerator-${var.environment}"
    Function = "ThumbnailGenerator"
  }
}

# SQS event source mapping for ThumbnailGenerator
resource "aws_lambda_event_source_mapping" "thumbnail_sqs" {
  event_source_arn                   = var.media_processing_queue_arn
  function_name                      = aws_lambda_function.thumbnail_generator.arn
  batch_size                         = 5
  maximum_batching_window_in_seconds = 10
  function_response_types            = ["ReportBatchItemFailures"]
}

# 2. ContentModerator — S3 event-triggered (via SQS)
resource "aws_lambda_function" "content_moderator" {
  function_name = "${var.project_name}-ContentModerator-${var.environment}"
  role          = aws_iam_role.lambda_execution.arn
  handler       = "MediaFlows.Lambda.ContentModerator::MediaFlows.Lambda.ContentModerator.Function::FunctionHandler"
  runtime       = "dotnet8"
  architectures = ["arm64"]
  memory_size   = var.lambda_memory_size
  timeout       = var.lambda_timeout

  filename         = data.archive_file.lambda_placeholder.output_path
  source_code_hash = data.archive_file.lambda_placeholder.output_base64sha256

  vpc_config {
    subnet_ids         = var.app_subnet_ids
    security_group_ids = [var.lambda_sg_id]
  }

  environment {
    variables = {
      CONTENT_FLAGGED_TOPIC_ARN = var.content_flagged_topic_arn
      BUCKET_NAME               = var.s3_bucket_name
      DB_CONNECTION_STRING      = var.db_connection_string
    }
  }

  tracing_config {
    mode = "Active"
  }

  tags = {
    Name     = "${var.project_name}-ContentModerator-${var.environment}"
    Function = "ContentModerator"
  }
}

resource "aws_lambda_event_source_mapping" "content_moderator_sqs" {
  event_source_arn                   = var.content_moderation_queue_arn
  function_name                      = aws_lambda_function.content_moderator.arn
  batch_size                         = 1
  maximum_batching_window_in_seconds = 0
  function_response_types            = ["ReportBatchItemFailures"]
}

# 3. NotificationDispatcher — EventBridge-triggered
resource "aws_lambda_function" "notification_dispatcher" {
  function_name = "${var.project_name}-NotificationDispatcher-${var.environment}"
  role          = aws_iam_role.lambda_execution.arn
  handler       = "MediaFlows.Lambda.NotificationDispatcher::MediaFlows.Lambda.NotificationDispatcher.Function::FunctionHandler"
  runtime       = "dotnet8"
  architectures = ["arm64"]
  memory_size   = 256
  timeout       = 30

  filename         = data.archive_file.lambda_placeholder.output_path
  source_code_hash = data.archive_file.lambda_placeholder.output_base64sha256

  environment {
    variables = {
      NOTIFICATION_TOPIC_ARN = var.notification_topic_arn
    }
  }

  tracing_config {
    mode = "Active"
  }

  tags = {
    Name     = "${var.project_name}-NotificationDispatcher-${var.environment}"
    Function = "NotificationDispatcher"
  }
}

# 4. SearchApi — API Gateway-triggered (VPC-attached for RDS access)
resource "aws_lambda_function" "search_api" {
  function_name = "${var.project_name}-SearchApi-${var.environment}"
  role          = aws_iam_role.lambda_execution.arn
  handler       = "MediaFlows.Lambda.SearchApi::MediaFlows.Lambda.SearchApi.Function::FunctionHandler"
  runtime       = "dotnet8"
  architectures = ["arm64"]
  memory_size   = var.lambda_memory_size
  timeout       = 29 # API Gateway has 29s hard limit

  filename         = data.archive_file.lambda_placeholder.output_path
  source_code_hash = data.archive_file.lambda_placeholder.output_base64sha256

  vpc_config {
    subnet_ids         = var.app_subnet_ids
    security_group_ids = [var.lambda_sg_id]
  }

  environment {
    variables = {
      DB_CONNECTION_STRING = var.db_connection_string
    }
  }

  tracing_config {
    mode = "Active"
  }

  tags = {
    Name     = "${var.project_name}-SearchApi-${var.environment}"
    Function = "SearchApi"
  }
}

# 5. AnalyticsAggregator — EventBridge scheduled cron (nightly aggregation only)
resource "aws_lambda_function" "analytics_aggregator" {
  function_name = "${var.project_name}-AnalyticsAggregator-${var.environment}"
  role          = aws_iam_role.lambda_execution.arn
  handler       = "MediaFlows.Lambda.AnalyticsAggregator::MediaFlows.Lambda.AnalyticsAggregator.Function::FunctionHandler"
  runtime       = "dotnet8"
  architectures = ["arm64"]
  memory_size   = 256
  timeout       = 300

  filename         = data.archive_file.lambda_placeholder.output_path
  source_code_hash = data.archive_file.lambda_placeholder.output_base64sha256

  environment {
    variables = {
      ENVIRONMENT           = var.environment
      DYNAMODB_TABLE_PREFIX = "${var.project_name}-"
    }
  }

  tracing_config {
    mode = "Active"
  }

  tags = {
    Name     = "${var.project_name}-AnalyticsAggregator-${var.environment}"
    Function = "AnalyticsAggregator"
  }
}

# 5b. TrendingApi — API Gateway-backed read endpoint (split from AnalyticsAggregator)
resource "aws_lambda_function" "trending_api" {
  function_name = "${var.project_name}-TrendingApi-${var.environment}"
  role          = aws_iam_role.lambda_execution.arn
  handler       = "MediaFlows.Lambda.TrendingApi::MediaFlows.Lambda.TrendingApi.Function::FunctionHandler"
  runtime       = "dotnet8"
  architectures = ["arm64"]
  memory_size   = 256
  timeout       = 30

  filename         = data.archive_file.lambda_placeholder.output_path
  source_code_hash = data.archive_file.lambda_placeholder.output_base64sha256

  environment {
    variables = {
      ENVIRONMENT           = var.environment
      DYNAMODB_TABLE_PREFIX = "${var.project_name}-"
    }
  }

  tracing_config {
    mode = "Active"
  }

  tags = {
    Name     = "${var.project_name}-TrendingApi-${var.environment}"
    Function = "TrendingApi"
  }
}

# 6. PostConfirmationGroupAssigner — Cognito post-confirmation trigger
# Adds self-registered users to the default "Viewer" group after they confirm
# their email. Invocation permission and wiring to the Cognito user pool are
# defined in the root module to avoid a circular dependency between the auth
# and serverless modules.
resource "aws_lambda_function" "post_confirmation_group_assigner" {
  function_name = "${var.project_name}-PostConfirmationGroupAssigner-${var.environment}"
  role          = aws_iam_role.lambda_execution.arn
  handler       = "MediaFlows.Lambda.PostConfirmationGroupAssigner::MediaFlows.Lambda.PostConfirmationGroupAssigner.Function::FunctionHandler"
  runtime       = "dotnet8"
  architectures = ["arm64"]
  memory_size   = 256
  timeout       = 10

  filename         = data.archive_file.lambda_placeholder.output_path
  source_code_hash = data.archive_file.lambda_placeholder.output_base64sha256

  tracing_config {
    mode = "Active"
  }

  tags = {
    Name     = "${var.project_name}-PostConfirmationGroupAssigner-${var.environment}"
    Function = "PostConfirmationGroupAssigner"
  }
}

# ──────────────────────────────────────────────────
# API Gateway REST API
# ──────────────────────────────────────────────────
resource "aws_api_gateway_rest_api" "main" {
  name        = "${var.project_name}-api-${var.environment}"
  description = "MediaFlows REST API - ${var.environment}"

  endpoint_configuration {
    types = ["REGIONAL"]
  }

  tags = {
    Name = "${var.project_name}-api-${var.environment}"
  }
}

resource "aws_api_gateway_resource" "api" {
  rest_api_id = aws_api_gateway_rest_api.main.id
  parent_id   = aws_api_gateway_rest_api.main.root_resource_id
  path_part   = "api"
}

resource "aws_api_gateway_resource" "search" {
  rest_api_id = aws_api_gateway_rest_api.main.id
  parent_id   = aws_api_gateway_resource.api.id
  path_part   = "search"
}

resource "aws_api_gateway_method" "search_get" {
  rest_api_id   = aws_api_gateway_rest_api.main.id
  resource_id   = aws_api_gateway_resource.search.id
  http_method   = "GET"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "search_lambda" {
  rest_api_id             = aws_api_gateway_rest_api.main.id
  resource_id             = aws_api_gateway_resource.search.id
  http_method             = aws_api_gateway_method.search_get.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = aws_lambda_function.search_api.invoke_arn
}

resource "aws_api_gateway_resource" "analytics" {
  rest_api_id = aws_api_gateway_rest_api.main.id
  parent_id   = aws_api_gateway_resource.api.id
  path_part   = "analytics"
}

resource "aws_api_gateway_resource" "trending" {
  rest_api_id = aws_api_gateway_rest_api.main.id
  parent_id   = aws_api_gateway_resource.analytics.id
  path_part   = "trending"
}

resource "aws_api_gateway_method" "trending_get" {
  rest_api_id   = aws_api_gateway_rest_api.main.id
  resource_id   = aws_api_gateway_resource.trending.id
  http_method   = "GET"
  authorization = "NONE"
}

resource "aws_api_gateway_integration" "trending_lambda" {
  rest_api_id             = aws_api_gateway_rest_api.main.id
  resource_id             = aws_api_gateway_resource.trending.id
  http_method             = aws_api_gateway_method.trending_get.http_method
  integration_http_method = "POST"
  type                    = "AWS_PROXY"
  uri                     = aws_lambda_function.trending_api.invoke_arn
}

# API Gateway deployment
resource "aws_api_gateway_deployment" "main" {
  rest_api_id = aws_api_gateway_rest_api.main.id

  depends_on = [
    aws_api_gateway_integration.search_lambda,
    aws_api_gateway_integration.trending_lambda,
  ]

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_api_gateway_stage" "main" {
  deployment_id = aws_api_gateway_deployment.main.id
  rest_api_id   = aws_api_gateway_rest_api.main.id
  stage_name    = var.environment

  xray_tracing_enabled = true

  tags = {
    Name = "${var.project_name}-api-stage-${var.environment}"
  }
}

# Lambda permissions for API Gateway
resource "aws_lambda_permission" "search_api_gw" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.search_api.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.main.execution_arn}/*/*"
}

resource "aws_lambda_permission" "trending_api_gw" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.trending_api.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_api_gateway_rest_api.main.execution_arn}/*/*"
}
