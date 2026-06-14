# infrastructure/modules/compute/main.tf

# ──────────────────────────────────────────────────
# IAM Role for EC2 Instance Profile
# ──────────────────────────────────────────────────
resource "aws_iam_role" "eb_ec2_role" {
  name = "${var.project_name}-eb-ec2-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ec2.amazonaws.com"
        }
      }
    ]
  })

  tags = {
    Name = "${var.project_name}-eb-ec2-role-${var.environment}"
  }
}

resource "aws_iam_role_policy_attachment" "eb_web_tier" {
  role       = aws_iam_role.eb_ec2_role.name
  policy_arn = "arn:aws:iam::aws:policy/AWSElasticBeanstalkWebTier"
}

resource "aws_iam_role_policy_attachment" "eb_worker_tier" {
  role       = aws_iam_role.eb_ec2_role.name
  policy_arn = "arn:aws:iam::aws:policy/AWSElasticBeanstalkWorkerTier"
}

resource "aws_iam_role_policy_attachment" "cloudwatch_agent" {
  role       = aws_iam_role.eb_ec2_role.name
  policy_arn = "arn:aws:iam::aws:policy/CloudWatchAgentServerPolicy"
}

resource "aws_iam_role_policy_attachment" "xray_daemon" {
  role       = aws_iam_role.eb_ec2_role.name
  policy_arn = "arn:aws:iam::aws:policy/AWSXRayDaemonWriteAccess"
}

# Enables `aws ssm send-command` against the EB instance — used to run
# DB maintenance such as TRUNCATE/seed against PostgreSQL (RDS is private,
# EB EC2 is the in-VPC entry point) and for ad-hoc DB ops.
resource "aws_iam_role_policy_attachment" "ssm_managed_instance" {
  role       = aws_iam_role.eb_ec2_role.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

resource "aws_iam_role_policy" "eb_app_policy" {
  name = "${var.project_name}-eb-app-policy-${var.environment}"
  role = aws_iam_role.eb_ec2_role.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "S3MediaAccess"
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:PutObject",
          "s3:DeleteObject",
          "s3:ListBucket",
          "s3:GetObjectMetadata"
        ]
        Resource = [
          "arn:aws:s3:::${var.s3_bucket_name}",
          "arn:aws:s3:::${var.s3_bucket_name}/*"
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
          "dynamodb:Scan"
        ]
        Resource = "arn:aws:dynamodb:*:*:table/${var.project_name}-*"
      },
      {
        Sid    = "CognitoAccess"
        Effect = "Allow"
        Action = [
          "cognito-idp:AdminGetUser",
          "cognito-idp:AdminListGroupsForUser",
          "cognito-idp:ListUsers",
          "cognito-idp:AdminCreateUser",
          "cognito-idp:AdminAddUserToGroup",
          "cognito-idp:AdminRemoveUserFromGroup",
          "cognito-idp:AdminUpdateUserAttributes",
          "cognito-idp:AdminDeleteUser",
          "cognito-idp:AdminDisableUser",
          "cognito-idp:AdminEnableUser"
        ]
        Resource = "*"
      },
      {
        Sid    = "SNSPublish"
        Effect = "Allow"
        Action = [
          "sns:Publish",
          "sns:Subscribe"
        ]
        Resource = "arn:aws:sns:*:*:${var.project_name}-*"
      },
      {
        Sid    = "EventBridgePutEvents"
        Effect = "Allow"
        Action = [
          "events:PutEvents"
        ]
        Resource = "arn:aws:events:*:*:event-bus/${var.project_name}-*"
      },
      {
        Sid    = "SQSAccess"
        Effect = "Allow"
        Action = [
          "sqs:SendMessage",
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:GetQueueAttributes"
        ]
        Resource = "arn:aws:sqs:*:*:${var.project_name}-*"
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
        Sid    = "SSMParameterStore"
        Effect = "Allow"
        Action = [
          "ssm:GetParameter",
          "ssm:GetParametersByPath",
          "ssm:PutParameter",
          "ssm:DeleteParameter"
        ]
        Resource = "arn:aws:ssm:*:*:parameter/MediaFlows/*"
      },
      {
        Sid    = "SecretsManagerAccess"
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue"
        ]
        Resource = "arn:aws:secretsmanager:*:*:secret:MediaFlows/*"
      },
      {
        Sid    = "CloudWatchLogs"
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents",
          "logs:DescribeLogGroups",
          "logs:DescribeLogStreams",
          "logs:StartQuery",
          "logs:GetQueryResults",
          "logs:StopQuery"
        ]
        Resource = "*"
      },
      {
        Sid    = "CloudWatchMetrics"
        Effect = "Allow"
        Action = [
          "cloudwatch:GetMetricData",
          "cloudwatch:ListMetrics",
          "cloudwatch:DescribeAlarms"
        ]
        Resource = "*"
      },
      {
        Sid    = "CostExplorerRead"
        Effect = "Allow"
        Action = [
          "ce:GetCostAndUsage"
        ]
        Resource = "*"
      }
    ]
  })
}

resource "aws_iam_instance_profile" "eb_ec2_profile" {
  name = "${var.project_name}-eb-ec2-profile-${var.environment}"
  role = aws_iam_role.eb_ec2_role.name
}

# ──────────────────────────────────────────────────
# IAM Service Role for Elastic Beanstalk
# ──────────────────────────────────────────────────
resource "aws_iam_role" "eb_service_role" {
  name = "${var.project_name}-eb-service-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "elasticbeanstalk.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "eb_enhanced_health" {
  role       = aws_iam_role.eb_service_role.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSElasticBeanstalkEnhancedHealth"
}

resource "aws_iam_role_policy_attachment" "eb_managed_updates" {
  role       = aws_iam_role.eb_service_role.name
  policy_arn = "arn:aws:iam::aws:policy/AWSElasticBeanstalkManagedUpdatesCustomerRolePolicy"
}

# ──────────────────────────────────────────────────
# Elastic Beanstalk Application
# ──────────────────────────────────────────────────
resource "aws_elastic_beanstalk_application" "main" {
  count = var.enabled ? 1 : 0

  name        = "${var.project_name}-app"
  description = "MediaFlows ASP.NET Core MVC application"

  appversion_lifecycle {
    service_role          = aws_iam_role.eb_service_role.arn
    max_count             = 10
    delete_source_from_s3 = true
  }
}

# ──────────────────────────────────────────────────
# Self-signed TLS certificate for ALB HTTPS listener
# Only created when no ACM certificate ARN is provided (dev environment)
# ──────────────────────────────────────────────────
resource "tls_private_key" "alb" {
  count     = var.app_domain == "" ? 1 : 0
  algorithm = "RSA"
  rsa_bits  = 2048
}

resource "tls_self_signed_cert" "alb" {
  count           = var.app_domain == "" ? 1 : 0
  private_key_pem = tls_private_key.alb[0].private_key_pem

  subject {
    common_name  = "${var.project_name}-${var.environment}.elasticbeanstalk.com"
    organization = "MediaFlows"
  }

  validity_period_hours = 8760 # 1 year

  allowed_uses = [
    "key_encipherment",
    "digital_signature",
    "server_auth",
  ]
}

resource "aws_acm_certificate" "alb" {
  count            = var.app_domain == "" ? 1 : 0
  private_key      = tls_private_key.alb[0].private_key_pem
  certificate_body = tls_self_signed_cert.alb[0].cert_pem

  tags = {
    Name = "${var.project_name}-alb-cert-${var.environment}"
  }
}

locals {
  alb_certificate_arn = var.app_domain != "" ? var.acm_certificate_arn : aws_acm_certificate.alb[0].arn
}

# ──────────────────────────────────────────────────
# Elastic Beanstalk Environment
# ──────────────────────────────────────────────────
resource "aws_elastic_beanstalk_environment" "main" {
  count = var.enabled ? 1 : 0

  name        = "${var.project_name}-${var.environment}"
  application = aws_elastic_beanstalk_application.main[0].name
  # AWS EB retires minor versions of this stack regularly. When the deploy
  # fails with "No Solution Stack named ...", check current availability via:
  #   aws elasticbeanstalk list-available-solution-stacks \
  #     --region ap-southeast-1 \
  #     --query 'SolutionStacks[?contains(@, `.NET 8`)]'
  solution_stack_name = "64bit Amazon Linux 2023 v3.10.3 running .NET 8"
  tier                = "WebServer"

  # VPC configuration
  setting {
    namespace = "aws:ec2:vpc"
    name      = "VPCId"
    value     = var.vpc_id
  }

  setting {
    namespace = "aws:ec2:vpc"
    name      = "Subnets"
    value     = join(",", var.app_subnet_ids)
  }

  setting {
    namespace = "aws:ec2:vpc"
    name      = "ELBSubnets"
    value     = join(",", var.public_subnet_ids)
  }

  setting {
    namespace = "aws:ec2:vpc"
    name      = "ELBScheme"
    value     = "public"
  }

  # Instance configuration (Launch Template — Launch Configurations are deprecated)
  setting {
    namespace = "aws:autoscaling:launchconfiguration"
    name      = "InstanceType"
    value     = var.instance_type
  }

  setting {
    namespace = "aws:autoscaling:launchconfiguration"
    name      = "IamInstanceProfile"
    value     = aws_iam_instance_profile.eb_ec2_profile.name
  }

  setting {
    namespace = "aws:autoscaling:launchconfiguration"
    name      = "SecurityGroups"
    value     = var.app_security_group_id
  }

  setting {
    namespace = "aws:autoscaling:launchconfiguration"
    name      = "DisableIMDSv1"
    value     = "true"
  }

  # Auto-scaling
  setting {
    namespace = "aws:autoscaling:asg"
    name      = "MinSize"
    value     = tostring(var.min_instances)
  }

  setting {
    namespace = "aws:autoscaling:asg"
    name      = "MaxSize"
    value     = tostring(var.max_instances)
  }

  setting {
    namespace = "aws:autoscaling:trigger"
    name      = "MeasureName"
    value     = "CPUUtilization"
  }

  setting {
    namespace = "aws:autoscaling:trigger"
    name      = "Statistic"
    value     = "Average"
  }

  setting {
    namespace = "aws:autoscaling:trigger"
    name      = "Unit"
    value     = "Percent"
  }

  setting {
    namespace = "aws:autoscaling:trigger"
    name      = "UpperThreshold"
    value     = "70"
  }

  setting {
    namespace = "aws:autoscaling:trigger"
    name      = "LowerThreshold"
    value     = "30"
  }

  # Load balancer
  setting {
    namespace = "aws:elasticbeanstalk:environment"
    name      = "EnvironmentType"
    value     = "LoadBalanced"
  }

  setting {
    namespace = "aws:elasticbeanstalk:environment"
    name      = "LoadBalancerType"
    value     = "application"
  }

  setting {
    namespace = "aws:elasticbeanstalk:environment"
    name      = "ServiceRole"
    value     = aws_iam_role.eb_service_role.arn
  }

  setting {
    namespace = "aws:elbv2:loadbalancer"
    name      = "SecurityGroups"
    value     = var.alb_security_group_id
  }

  # Default process — backend instances listen on HTTP port 80
  setting {
    namespace = "aws:elasticbeanstalk:environment:process:default"
    name      = "Port"
    value     = "80"
  }

  setting {
    namespace = "aws:elasticbeanstalk:environment:process:default"
    name      = "Protocol"
    value     = "HTTP"
  }

  # HTTP listener on port 80 — redirect to HTTPS
  setting {
    namespace = "aws:elbv2:listener:default"
    name      = "ListenerEnabled"
    value     = "true"
  }

  # HTTPS listener on port 443
  setting {
    namespace = "aws:elbv2:listener:443"
    name      = "ListenerEnabled"
    value     = "true"
  }

  setting {
    namespace = "aws:elbv2:listener:443"
    name      = "Protocol"
    value     = "HTTPS"
  }

  setting {
    namespace = "aws:elbv2:listener:443"
    name      = "SSLCertificateArns"
    value     = local.alb_certificate_arn
  }

  # Environment variables
  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "ASPNETCORE_ENVIRONMENT"
    value     = var.environment == "prod" ? "Production" : "Development"
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "ConnectionStrings__PostgreSQL"
    value     = var.db_connection_string
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "AWS__Region"
    value     = "ap-southeast-1"
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "S3__BucketName"
    value     = var.s3_bucket_name
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "S3__CloudFrontDomain"
    value     = var.cloudfront_domain
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "Cognito__Authority"
    value     = var.cognito_authority
  }

  # Program.cs reads Jwt:Authority for the JWT Bearer middleware. Mirroring
  # Cognito.Authority into Jwt__Authority via EB env keeps the value
  # account-agnostic — appsettings.Production.json no longer needs the
  # per-account Cognito pool URL hardcoded.
  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "Jwt__Authority"
    value     = var.cognito_authority
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "Cognito__ClientId"
    value     = var.cognito_client_id
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "Cognito__ClientSecret"
    value     = var.cognito_client_secret
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "Cognito__Domain"
    value     = var.cognito_domain
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "Cognito__UserPoolId"
    value     = var.cognito_user_pool_id
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "DynamoDB__TableNamePrefix"
    value     = "${var.project_name}-"
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "EventBridge__BusName"
    value     = var.event_bus_name
  }

  # Rekognition pipeline
  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "Rekognition__Enabled"
    value     = var.asset_uploaded_topic_arn != "" ? "true" : "false"
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "Rekognition__BucketName"
    value     = var.s3_bucket_name
  }

  setting {
    namespace = "aws:elasticbeanstalk:application:environment"
    name      = "Rekognition__AssetUploadedTopicArn"
    value     = var.asset_uploaded_topic_arn
  }

  # Custom domain (when configured)
  dynamic "setting" {
    for_each = var.app_domain != "" ? [1] : []
    content {
      namespace = "aws:elasticbeanstalk:application:environment"
      name      = "App__BaseUrl"
      value     = "https://${var.app_domain}"
    }
  }

  # Health reporting
  setting {
    namespace = "aws:elasticbeanstalk:healthreporting:system"
    name      = "SystemType"
    value     = "enhanced"
  }

  # ALB target-group health check — our API has no route at `/`, so point
  # the probe at /api/health (returns a 200 JSON status from a dedicated
  # minimal endpoint).
  setting {
    namespace = "aws:elasticbeanstalk:environment:process:default"
    name      = "HealthCheckPath"
    value     = "/api/health"
  }

  setting {
    namespace = "aws:elasticbeanstalk:environment:process:default"
    name      = "MatcherHTTPCode"
    value     = "200"
  }

  # X-Ray
  setting {
    namespace = "aws:elasticbeanstalk:xray"
    name      = "XRayEnabled"
    value     = "true"
  }

  # Managed updates
  setting {
    namespace = "aws:elasticbeanstalk:managedactions"
    name      = "ManagedActionsEnabled"
    value     = "true"
  }

  setting {
    namespace = "aws:elasticbeanstalk:managedactions"
    name      = "PreferredStartTime"
    value     = "Sun:04:00"
  }

  setting {
    namespace = "aws:elasticbeanstalk:managedactions:platformupdate"
    name      = "UpdateLevel"
    value     = "minor"
  }

  # Deployment policy
  setting {
    namespace = "aws:elasticbeanstalk:command"
    name      = "DeploymentPolicy"
    value     = "Rolling"
  }

  setting {
    namespace = "aws:elasticbeanstalk:command"
    name      = "BatchSizeType"
    value     = "Percentage"
  }

  setting {
    namespace = "aws:elasticbeanstalk:command"
    name      = "BatchSize"
    value     = "50"
  }

  tags = {
    Name = "${var.project_name}-eb-env-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# ALB data source — for Route53 alias records
# ──────────────────────────────────────────────────
data "aws_lb" "eb_alb" {
  count = var.enabled ? 1 : 0

  arn = one(aws_elastic_beanstalk_environment.main[0].load_balancers)
}
