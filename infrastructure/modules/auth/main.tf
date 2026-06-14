# infrastructure/modules/auth/main.tf

# ──────────────────────────────────────────────────
# Cognito User Pool
# ──────────────────────────────────────────────────
resource "aws_cognito_user_pool" "main" {
  name = "${var.project_name}-users-${var.environment}"

  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]

  password_policy {
    minimum_length                   = 8
    require_lowercase                = true
    require_numbers                  = true
    require_symbols                  = true
    require_uppercase                = true
    temporary_password_validity_days = 7
  }

  schema {
    name                     = "email"
    attribute_data_type      = "String"
    developer_only_attribute = false
    mutable                  = true
    required                 = true

    string_attribute_constraints {
      min_length = 1
      max_length = 256
    }
  }

  schema {
    name                     = "name"
    attribute_data_type      = "String"
    developer_only_attribute = false
    mutable                  = true
    required                 = true

    string_attribute_constraints {
      min_length = 1
      max_length = 256
    }
  }

  account_recovery_setting {
    recovery_mechanism {
      name     = "verified_email"
      priority = 1
    }
  }

  email_configuration {
    email_sending_account = "COGNITO_DEFAULT"
  }

  mfa_configuration = "OFF" # Keep simple for academic project

  # Post-confirmation trigger — assigns self-registered users to the default
  # "Viewer" group. Attached conditionally so the initial Terraform apply can
  # succeed even when the Lambda ARN isn't available yet (first-time bootstrap).
  dynamic "lambda_config" {
    for_each = var.post_confirmation_lambda_arn != "" ? [1] : []
    content {
      post_confirmation = var.post_confirmation_lambda_arn
    }
  }

  tags = {
    Name = "${var.project_name}-user-pool-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# Cognito User Pool Domain (for Hosted UI)
# ──────────────────────────────────────────────────

# Prefix-based domain (dev or when no custom domain is set)
resource "aws_cognito_user_pool_domain" "prefix" {
  count        = var.custom_domain == "" ? 1 : 0
  domain       = "${var.project_name}-${var.environment}"
  user_pool_id = aws_cognito_user_pool.main.id
}

# Custom domain (prod with custom domain)
resource "aws_cognito_user_pool_domain" "custom" {
  count           = var.custom_domain != "" ? 1 : 0
  domain          = var.custom_domain
  certificate_arn = var.acm_certificate_arn
  user_pool_id    = aws_cognito_user_pool.main.id
}

# ──────────────────────────────────────────────────
# User Pool Groups (4 roles)
# ──────────────────────────────────────────────────
resource "aws_cognito_user_group" "system_admin" {
  name         = "SystemAdmin"
  user_pool_id = aws_cognito_user_pool.main.id
  description  = "System administrators — platform config, user management, monitoring"
  precedence   = 1
}

resource "aws_cognito_user_group" "content_creator" {
  name         = "ContentCreator"
  user_pool_id = aws_cognito_user_pool.main.id
  description  = "Content creators — media upload, asset management, tagging"
  precedence   = 2
}

resource "aws_cognito_user_group" "editor" {
  name         = "Editor"
  user_pool_id = aws_cognito_user_pool.main.id
  description  = "Editors/reviewers — review queue, approve/reject, scheduling"
  precedence   = 3
}

resource "aws_cognito_user_group" "viewer" {
  name         = "Viewer"
  user_pool_id = aws_cognito_user_pool.main.id
  description  = "Viewers/consumers — browse, search, bookmark, comment"
  precedence   = 4
}

# ──────────────────────────────────────────────────
# App Client (OIDC Authorization Code Flow with PKCE)
# ──────────────────────────────────────────────────
resource "aws_cognito_user_pool_client" "web_app" {
  name         = "${var.project_name}-web-${var.environment}"
  user_pool_id = aws_cognito_user_pool.main.id

  # Generate client secret (required for server-side OIDC)
  generate_secret = true

  # OAuth 2.0 configuration
  allowed_oauth_flows                  = ["code"]
  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_scopes                 = ["openid", "email", "profile"]
  supported_identity_providers         = ["COGNITO"]

  # URLs
  callback_urls = var.callback_urls
  logout_urls   = var.logout_urls

  # Token expiration
  access_token_validity  = 60 # 60 minutes
  id_token_validity      = 60 # 60 minutes
  refresh_token_validity = 30 # 30 days

  token_validity_units {
    access_token  = "minutes"
    id_token      = "minutes"
    refresh_token = "days"
  }

  # Security settings
  prevent_user_existence_errors = "ENABLED"
  explicit_auth_flows = [
    "ALLOW_REFRESH_TOKEN_AUTH",
    "ALLOW_USER_SRP_AUTH",
    "ALLOW_USER_PASSWORD_AUTH",
  ]
}
