# infrastructure/main.tf
# Root module — composes all infrastructure modules

# ──────────────────────────────────────────────────
# Networking (no dependencies)
# ──────────────────────────────────────────────────
module "networking" {
  source       = "./modules/networking"
  environment  = var.environment
  project_name = var.project_name

  nat_enabled = var.services_enabled
}

# ──────────────────────────────────────────────────
# Storage (no dependencies)
# ──────────────────────────────────────────────────
module "storage" {
  source               = "./modules/storage"
  environment          = var.environment
  project_name         = var.project_name
  cors_allowed_origins = var.cors_allowed_origins
}

# ──────────────────────────────────────────────────
# Auth (no dependencies)
# ──────────────────────────────────────────────────
module "auth" {
  source       = "./modules/auth"
  environment  = var.environment
  project_name = var.project_name

  callback_urls = var.cognito_callback_urls
  logout_urls   = var.cognito_logout_urls
  # Another AWS account may still hold a global claim on auth.${domain} and
  # cdn./www. on CloudFront. Until any such claim is released, fall back to
  # login.${domain} for the Cognito hosted UI. The frontend also lives at
  # app.${domain} via the amplify module override below.
  custom_domain       = var.domain_name != "" ? "login.${var.domain_name}" : ""
  acm_certificate_arn = var.domain_name != "" ? module.dns[0].acm_certificate_arn_cloudfront : ""

  # Post-confirmation trigger — wired from the serverless module. The Cognito
  # user pool resource (aws_cognito_user_pool.main) is independent of any
  # module-level data the serverless module produces, so Terraform can resolve
  # this cross-module reference without a cycle.
  post_confirmation_lambda_arn = module.serverless.post_confirmation_group_assigner_arn
}

# ──────────────────────────────────────────────────
# DNS (no dependencies on other modules)
# ──────────────────────────────────────────────────
module "dns" {
  source = "./modules/dns"
  count  = var.domain_name != "" ? 1 : 0

  providers = {
    aws           = aws
    aws.us_east_1 = aws.us_east_1
  }

  domain_name         = var.domain_name
  environment         = var.environment
  project_name        = var.project_name
  manage_www_redirect = var.manage_www_redirect
}

# ──────────────────────────────────────────────────
# Database (depends on networking)
# ──────────────────────────────────────────────────
module "database" {
  source       = "./modules/database"
  environment  = var.environment
  project_name = var.project_name

  enabled             = var.services_enabled
  restore_snapshot_id = var.rds_restore_snapshot_id

  vpc_id               = module.networking.vpc_id
  db_subnet_ids        = module.networking.db_subnet_ids
  db_security_group_id = module.networking.db_sg_id
  db_instance_class    = var.db_instance_class
  db_allocated_storage = var.db_allocated_storage
  db_username          = var.db_username
}

# ──────────────────────────────────────────────────
# CDN (depends on storage)
# ──────────────────────────────────────────────────
module "cdn" {
  source       = "./modules/cdn"
  environment  = var.environment
  project_name = var.project_name

  s3_bucket_id                   = module.storage.bucket_id
  s3_bucket_arn                  = module.storage.bucket_arn
  s3_bucket_regional_domain_name = module.storage.bucket_regional_domain_name
  # Dead account holds the cdn.${domain} CloudFront CNAME claim. Pass
  # empty so the CDN distribution serves only via its raw cloudfront.net
  # domain until the claim ages out or AWS Support releases it.
  custom_domain       = ""
  acm_certificate_arn = ""
}

# ──────────────────────────────────────────────────
# Messaging (depends on storage only — no Lambda deps)
# ──────────────────────────────────────────────────
module "messaging" {
  source       = "./modules/messaging"
  environment  = var.environment
  project_name = var.project_name

  s3_bucket_arn      = module.storage.bucket_arn
  notification_email = var.notification_email
}

# ──────────────────────────────────────────────────
# Serverless (depends on networking, storage, database, messaging partial)
# ──────────────────────────────────────────────────
module "serverless" {
  source       = "./modules/serverless"
  environment  = var.environment
  project_name = var.project_name

  s3_bucket_name       = module.storage.bucket_name
  s3_bucket_arn        = module.storage.bucket_arn
  lambda_sg_id         = module.networking.lambda_sg_id
  app_subnet_ids       = module.networking.app_subnet_ids
  db_connection_string = module.database.connection_string
  lambda_memory_size   = var.lambda_memory_size
  lambda_timeout       = var.lambda_timeout

  # SNS topic ARNs — created by messaging module
  # To break circular dependency, we use the messaging module outputs
  content_flagged_topic_arn    = module.messaging.content_flagged_topic_arn
  notification_topic_arn       = module.messaging.review_completed_topic_arn
  media_processing_queue_arn   = module.messaging.media_processing_queue_arn
  content_moderation_queue_arn = module.messaging.content_moderation_queue_arn
}

# ──────────────────────────────────────────────────
# Compute (depends on networking, database, storage, auth)
# ──────────────────────────────────────────────────
module "compute" {
  source       = "./modules/compute"
  environment  = var.environment
  project_name = var.project_name

  enabled = var.services_enabled

  vpc_id                = module.networking.vpc_id
  app_subnet_ids        = module.networking.app_subnet_ids
  public_subnet_ids     = module.networking.public_subnet_ids
  app_security_group_id = module.networking.app_sg_id
  alb_security_group_id = module.networking.alb_sg_id

  db_connection_string  = module.database.connection_string
  s3_bucket_name        = module.storage.bucket_name
  cloudfront_domain     = module.cdn.cloudfront_domain_name
  cognito_authority     = module.auth.authority
  cognito_client_id     = module.auth.client_id
  cognito_client_secret = module.auth.client_secret
  cognito_domain        = module.auth.domain_url
  cognito_user_pool_id  = module.auth.user_pool_id

  instance_type       = var.eb_instance_type
  min_instances       = var.eb_min_instances
  max_instances       = var.eb_max_instances
  acm_certificate_arn = var.domain_name != "" ? module.dns[0].acm_certificate_arn_regional : ""
  app_domain          = var.domain_name

  asset_uploaded_topic_arn = module.messaging.asset_uploaded_topic_arn
  event_bus_name           = module.messaging.event_bus_name
}

# ──────────────────────────────────────────────────
# Monitoring (depends on compute, messaging, serverless)
# ──────────────────────────────────────────────────
module "monitoring" {
  source       = "./modules/monitoring"
  environment  = var.environment
  project_name = var.project_name

  eb_enabled = var.services_enabled

  system_alerts_topic_arn     = module.messaging.system_alerts_topic_arn
  eb_environment_name         = module.compute.eb_environment_name
  media_processing_queue_name = "${var.project_name}-media-processing-${var.environment}"

  lambda_function_names = [
    "${var.project_name}-ThumbnailGenerator-${var.environment}",
    "${var.project_name}-ContentModerator-${var.environment}",
    "${var.project_name}-NotificationDispatcher-${var.environment}",
    "${var.project_name}-SearchApi-${var.environment}",
    "${var.project_name}-AnalyticsAggregator-${var.environment}",
    "${var.project_name}-PostConfirmationGroupAssigner-${var.environment}",
  ]
}

# ──────────────────────────────────────────────────
# Amplify (depends on auth, cdn for env vars)
# ──────────────────────────────────────────────────
module "amplify" {
  source       = "./modules/amplify"
  aws_region   = var.aws_region
  environment  = var.environment
  project_name = var.project_name

  repository  = "https://github.com/${var.github_owner}/${var.github_repo}"
  branch_name = "main"

  domain_name   = var.domain_name
  custom_domain = var.domain_name != "" ? "app.${var.domain_name}" : ""
  frontend_url  = var.domain_name != "" ? "https://app.${var.domain_name}" : "https://${module.compute.eb_environment_url}"

  api_base_url         = var.domain_name != "" ? "https://api.${var.domain_name}" : "https://${module.compute.eb_environment_url}"
  cognito_client_id    = module.auth.client_id
  cognito_issuer       = module.auth.issuer
  cognito_user_pool_id = module.auth.user_pool_id
  cdn_url              = var.domain_name != "" ? "https://cdn.${var.domain_name}" : ""
}

# ──────────────────────────────────────────────────
# Route53 A-records (root level to avoid circular deps)
# ──────────────────────────────────────────────────

# Before cutover: apex domain → EB ALB
# After cutover:  apex domain → Amplify
# Toggle via var.amplify_cutover
resource "aws_route53_record" "root" {
  # Drop the apex record entirely when services are stopped (ALB doesn't exist).
  # Amplify cutover case stays; the cutover case doesn't reference module.compute.
  count   = var.domain_name != "" && (var.services_enabled || var.amplify_cutover) ? 1 : 0
  zone_id = module.dns[0].zone_id
  name    = var.domain_name
  type    = "A"

  # When Amplify cutover is active, use CNAME-style record to Amplify
  # When not active, alias to EB ALB
  dynamic "alias" {
    for_each = var.amplify_cutover ? [] : [1]
    content {
      name                   = module.compute.alb_dns_name
      zone_id                = module.compute.alb_zone_id
      evaluate_target_health = true
    }
  }

  # When cutover is active, alias the apex to the Amplify Hosting CloudFront
  # distribution. This is decoupled from `module.amplify` so cutover works
  # even when the Amplify Hosting app is managed outside Terraform.
  dynamic "alias" {
    for_each = var.amplify_cutover && var.amplify_cloudfront_dns != "" ? [1] : []
    content {
      name                   = var.amplify_cloudfront_dns
      zone_id                = "Z2FDTNDATAQYW2" # CloudFront global hosted zone ID
      evaluate_target_health = false
    }
  }
}

# api.<domain> → EB ALB (gated alongside compute — alb_dns_name
# is empty when services are stopped, so the alias would error otherwise)
resource "aws_route53_record" "api" {
  count   = var.domain_name != "" && var.services_enabled ? 1 : 0
  zone_id = module.dns[0].zone_id
  name    = "api.${var.domain_name}"
  type    = "A"

  alias {
    name                   = module.compute.alb_dns_name
    zone_id                = module.compute.alb_zone_id
    evaluate_target_health = true
  }
}

resource "aws_route53_record" "cdn" {
  count   = var.domain_name != "" ? 1 : 0
  zone_id = module.dns[0].zone_id
  name    = "cdn.${var.domain_name}"
  type    = "A"

  alias {
    name                   = module.cdn.cloudfront_domain_name
    zone_id                = module.cdn.cloudfront_hosted_zone_id
    evaluate_target_health = false
  }
}

# Mirror the Cognito user pool client's secret into SSM so frontend
# instrumentation.ts can populate process.env.COGNITO_CLIENT_SECRET at SSR
# cold start. The auth module rotates the secret whenever the client is
# replaced (via taint or other ForceNew change), and Terraform updates
# SSM in lockstep — no manual aws ssm put-parameter step needed after
# rotations.
resource "aws_ssm_parameter" "cognito_client_secret" {
  name        = "/${var.project_name}/${var.environment}/cognito-client-secret"
  description = "Cognito user pool client secret — read at SSR runtime by instrumentation.ts"
  type        = "SecureString"
  value       = module.auth.client_secret

  tags = {
    Name = "${var.project_name}-cognito-client-secret-${var.environment}"
  }
}

resource "aws_route53_record" "auth" {
  count   = var.domain_name != "" ? 1 : 0
  zone_id = module.dns[0].zone_id
  name    = "login.${var.domain_name}"
  type    = "A"

  alias {
    name                   = module.auth.cognito_custom_domain_cloudfront
    zone_id                = module.auth.cognito_custom_domain_cloudfront_zone_id
    evaluate_target_health = false
  }
}

# ──────────────────────────────────────────────────
# S3 Bucket Notification (root level — wires storage to messaging.
# Cannot live inside the storage module because messaging already
# consumes storage's bucket ARN, so the reverse direction would form
# a module-level cycle.)
#
# Fanning out to multiple consumers via SNS rather than putting two
# queue targets in the bucket notification directly: S3 rejects two
# notification rules with the same event and overlapping prefix. The
# asset_uploaded SNS topic uses raw_message_delivery=true on both
# subscriptions so the SQS consumers receive the S3 event JSON as-is.
# ──────────────────────────────────────────────────
resource "aws_s3_bucket_notification" "media_assets_uploads" {
  bucket = module.storage.bucket_id

  topic {
    id            = "asset-uploaded-fanout"
    topic_arn     = module.messaging.asset_uploaded_topic_arn
    events        = ["s3:ObjectCreated:*"]
    filter_prefix = "uploads/"
  }

  depends_on = [module.messaging]
}

# ──────────────────────────────────────────────────
# EventBridge Rules (root level — depends on both messaging + serverless)
# Defined here to avoid circular dependency between modules
# ──────────────────────────────────────────────────

# Daily Analytics Aggregation — cron(0 2 * * ? *)
resource "aws_cloudwatch_event_rule" "daily_analytics" {
  name                = "${var.project_name}-daily-analytics-${var.environment}"
  description         = "Trigger AnalyticsAggregator Lambda daily at 2 AM UTC"
  schedule_expression = "cron(0 2 * * ? *)"

  tags = {
    Name = "${var.project_name}-daily-analytics-${var.environment}"
  }
}

resource "aws_cloudwatch_event_target" "daily_analytics" {
  rule      = aws_cloudwatch_event_rule.daily_analytics.name
  target_id = "AnalyticsAggregator"
  arn       = module.serverless.analytics_aggregator_arn
}

resource "aws_lambda_permission" "eventbridge_analytics" {
  statement_id  = "AllowEventBridgeAnalyticsInvoke"
  action        = "lambda:InvokeFunction"
  function_name = module.serverless.analytics_aggregator_arn
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.daily_analytics.arn
}

# Rule: approval events → NotificationDispatcher
resource "aws_cloudwatch_event_rule" "approval_events" {
  name           = "${var.project_name}-approval-events-${var.environment}"
  description    = "Route review notification events to NotificationDispatcher"
  event_bus_name = module.messaging.event_bus_name

  event_pattern = jsonencode({
    source = ["mediaflows.reviews"]
  })

  tags = {
    Name = "${var.project_name}-approval-events-${var.environment}"
  }
}

resource "aws_cloudwatch_event_target" "approval_notification" {
  rule           = aws_cloudwatch_event_rule.approval_events.name
  event_bus_name = module.messaging.event_bus_name
  target_id      = "NotificationDispatcher"
  arn            = module.serverless.notification_dispatcher_arn
}

resource "aws_lambda_permission" "eventbridge_notification" {
  statement_id  = "AllowEventBridgeApprovalInvoke"
  action        = "lambda:InvokeFunction"
  function_name = module.serverless.notification_dispatcher_arn
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.approval_events.arn
}

# ──────────────────────────────────────────────────
# Cognito Post-Confirmation Trigger (root level — needs both auth and
# serverless module outputs; placing this at root avoids a module-level cycle)
# ──────────────────────────────────────────────────

# Extra inline policy on the Lambda execution role granting permission to
# add users to Cognito groups. Scoped to the specific user pool ARN.
resource "aws_iam_role_policy" "lambda_cognito_admin" {
  name = "${var.project_name}-lambda-cognito-admin-${var.environment}"
  role = module.serverless.lambda_execution_role_name

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid      = "CognitoAdminAddUserToGroup"
        Effect   = "Allow"
        Action   = "cognito-idp:AdminAddUserToGroup"
        Resource = module.auth.user_pool_arn
      }
    ]
  })
}

# Allow the Cognito user pool to invoke the PostConfirmationGroupAssigner Lambda.
resource "aws_lambda_permission" "cognito_post_confirmation" {
  statement_id  = "AllowCognitoPostConfirmationInvoke"
  action        = "lambda:InvokeFunction"
  function_name = module.serverless.post_confirmation_group_assigner_arn
  principal     = "cognito-idp.amazonaws.com"
  source_arn    = module.auth.user_pool_arn
}
