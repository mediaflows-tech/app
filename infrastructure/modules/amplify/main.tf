# infrastructure/modules/amplify/main.tf

data "aws_ssm_parameter" "nextauth_secret" {
  name = "/${var.project_name}/${var.environment}/nextauth-secret"
}

data "aws_ssm_parameter" "github_pat" {
  name = "/${var.project_name}/${var.environment}/github-pat"
}

# Cognito values mirrored to SSM for SSR cold-start access
resource "aws_ssm_parameter" "cognito_client_id" {
  name        = "/${var.project_name}/${var.environment}/cognito-client-id"
  description = "Cognito User Pool app client ID — read at SSR runtime by instrumentation.ts"
  type        = "String"
  value       = var.cognito_client_id

  tags = {
    Name = "${var.project_name}-cognito-client-id-${var.environment}"
  }
}

resource "aws_ssm_parameter" "cognito_issuer" {
  name        = "/${var.project_name}/${var.environment}/cognito-issuer"
  description = "Cognito OIDC issuer URL — read at SSR runtime by instrumentation.ts"
  type        = "String"
  value       = var.cognito_issuer

  tags = {
    Name = "${var.project_name}-cognito-issuer-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# Amplify App
# ──────────────────────────────────────────────────
resource "aws_amplify_app" "frontend" {
  name       = "${var.project_name}-frontend-${var.environment}"
  repository = var.repository

  access_token = data.aws_ssm_parameter.github_pat.value

  build_spec = file("${path.module}/../../../frontend/amplify.yml")

  platform = "WEB_COMPUTE" # SSR support

  # Track these in Terraform so we don't get drift-driven destroys when an
  # operator (or this assistant) sets them via aws cli to fix runtime env
  # injection. Both point at the same role here — the role grants Amplify
  # service access to logs/SSM AND is the SSR Lambda execution role.
  iam_service_role_arn = aws_iam_role.amplify_ssr.arn
  compute_role_arn     = aws_iam_role.amplify_ssr.arn

  # Environment variables — only the public/non-sensitive ones. NEXTAUTH_SECRET,
  # AUTH_SECRET, COGNITO_CLIENT_SECRET are loaded at SSR cold start by
  # frontend/src/instrumentation.ts via SSM (see amplify_ssr role policy
  # below for the corresponding ssm:GetParametersByPath grant). This avoids
  # leaking secrets into Amplify env vars (visible in Amplify Console) and
  # supports rotation without redeploying.
  environment_variables = {
    AMPLIFY_MONOREPO_APP_ROOT        = "frontend"
    API_BASE_URL                     = var.api_base_url
    NEXTAUTH_URL                     = var.frontend_url
    AUTH_URL                         = var.frontend_url
    AUTH_TRUST_HOST                  = "true"
    COGNITO_CLIENT_ID                = var.cognito_client_id
    COGNITO_ISSUER                   = var.cognito_issuer
    NEXT_PUBLIC_COGNITO_USER_POOL_ID = var.cognito_user_pool_id
    NEXT_PUBLIC_COGNITO_CLIENT_ID    = var.cognito_client_id
    NEXT_PUBLIC_CDN_URL              = var.cdn_url
    NEXT_PUBLIC_SIGNALR_URL          = "${var.api_base_url}/hubs"
    SSM_SECRETS_PATH                 = "/${var.project_name}/${var.environment}/"
  }

  # Auto-build on push
  enable_auto_branch_creation = false
  enable_branch_auto_build    = true
  enable_branch_auto_deletion = false
  enable_basic_auth           = false

  # Custom rewrite rules — SPA fallback + API proxy
  custom_rule {
    source = "/<*>"
    status = "404-200"
    target = "/index.html"
  }

  tags = {
    Name = "${var.project_name}-amplify-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# Branch (main)
# ──────────────────────────────────────────────────
resource "aws_amplify_branch" "main" {
  app_id      = aws_amplify_app.frontend.id
  branch_name = var.branch_name

  framework = "Next.js - SSR"
  stage     = var.environment == "prod" ? "PRODUCTION" : "DEVELOPMENT"

  enable_auto_build = true

  environment_variables = {
    NODE_ENV = var.environment == "prod" ? "production" : "development"
  }

  tags = {
    Name = "${var.project_name}-branch-${var.branch_name}-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# Custom Domain (optional)
# ──────────────────────────────────────────────────
# Guarded by `manage_custom_domain` because on the new account the dead
# account still holds a global Amplify claim on example.com, which
# makes CreateDomainAssociation fail. The cutover flow in the root main.tf
# (amplify_cutover + amplify_cloudfront_dns) aliases Route53 directly at
# Amplify's CloudFront distribution without needing an official domain
# association, which is the recommended workaround while Amplify support
# releases the old account's claim.
resource "aws_amplify_domain_association" "main" {
  count = var.custom_domain != "" ? 1 : 0

  app_id      = aws_amplify_app.frontend.id
  domain_name = var.custom_domain

  sub_domain {
    branch_name = aws_amplify_branch.main.branch_name
    prefix      = ""
  }

  wait_for_verification = false
}

# ──────────────────────────────────────────────────
# IAM Role for Amplify SSR (Lambda@Edge execution)
# ──────────────────────────────────────────────────
resource "aws_iam_role" "amplify_ssr" {
  name = "${var.project_name}-amplify-ssr-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "amplify.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })

  tags = {
    Name = "${var.project_name}-amplify-ssr-role-${var.environment}"
  }
}

resource "aws_iam_role_policy" "amplify_ssr" {
  name = "${var.project_name}-amplify-ssr-policy-${var.environment}"
  role = aws_iam_role.amplify_ssr.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AmplifySSRLogging"
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = "arn:aws:logs:*:*:*"
      },
      {
        Sid    = "RuntimeSecretsRead"
        Effect = "Allow"
        Action = [
          "ssm:GetParameter",
          "ssm:GetParameters",
          "ssm:GetParametersByPath"
        ]
        Resource = "arn:aws:ssm:${var.aws_region}:*:parameter/${var.project_name}/${var.environment}/*"
      }
    ]
  })
}
