# infrastructure/bootstrap/main.tf
# Bootstrap stack — creates the primitives that must exist before the main
# infrastructure/ stack can run: S3 state bucket, GitHub OIDC provider,
# GitHub Actions IAM role, and SSM SecureString parameters for secrets.

data "aws_caller_identity" "current" {}

locals {
  account_id        = data.aws_caller_identity.current.account_id
  state_bucket_name = "${var.project_name}-tfstate-${local.account_id}"
  role_name         = "${var.project_name}-github-actions-${var.environment}"
  ssm_prefix        = "/${var.project_name}/${var.environment}"
}

# ──────────────────────────────────────────────────
# S3 state bucket for the parent infrastructure/ stack
# ──────────────────────────────────────────────────
resource "aws_s3_bucket" "tfstate" {
  bucket = local.state_bucket_name

  tags = {
    Name = local.state_bucket_name
  }
}

resource "aws_s3_bucket_versioning" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "tfstate" {
  bucket = aws_s3_bucket.tfstate.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# ──────────────────────────────────────────────────
# GitHub Actions OIDC provider
# ──────────────────────────────────────────────────
data "tls_certificate" "github" {
  url = "https://token.actions.githubusercontent.com/.well-known/openid-configuration"
}

resource "aws_iam_openid_connect_provider" "github" {
  url             = "https://token.actions.githubusercontent.com"
  client_id_list  = ["sts.amazonaws.com"]
  thumbprint_list = [data.tls_certificate.github.certificates[0].sha1_fingerprint]

  tags = {
    Name = "${var.project_name}-github-oidc-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# IAM role assumed by GitHub Actions via OIDC
# ──────────────────────────────────────────────────
resource "aws_iam_role" "github_actions" {
  name = local.role_name

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect    = "Allow"
        Principal = { Federated = aws_iam_openid_connect_provider.github.arn }
        Action    = "sts:AssumeRoleWithWebIdentity"
        Condition = {
          StringLike = {
            "token.actions.githubusercontent.com:sub" = "repo:${var.github_owner}/${var.github_repo}:*"
          }
          StringEquals = {
            "token.actions.githubusercontent.com:aud" = "sts.amazonaws.com"
          }
        }
      }
    ]
  })

  tags = {
    Name = "${local.role_name}-role"
  }
}

# ──────────────────────────────────────────────────
# SSM SecureString parameters — operator-supplied secrets
# `ignore_changes = [value]` means later in-place rotations done directly
# in SSM (or via AWS CLI) are not reverted by `terraform apply`.
# ──────────────────────────────────────────────────
resource "aws_ssm_parameter" "db_password" {
  name        = "${local.ssm_prefix}/db-password"
  description = "RDS master password for ${var.project_name} ${var.environment}"
  type        = "SecureString"
  value       = var.db_password

  tags = {
    Name = "${var.project_name}-db-password-${var.environment}"
  }

  lifecycle {
    ignore_changes = [value]
  }
}

resource "aws_ssm_parameter" "nextauth_secret" {
  name        = "${local.ssm_prefix}/nextauth-secret"
  description = "NextAuth.js JWT secret for ${var.project_name} ${var.environment}"
  type        = "SecureString"
  value       = var.nextauth_secret

  tags = {
    Name = "${var.project_name}-nextauth-secret-${var.environment}"
  }

  lifecycle {
    ignore_changes = [value]
  }
}

resource "aws_ssm_parameter" "github_pat" {
  name        = "${local.ssm_prefix}/github-pat"
  description = "GitHub PAT for Amplify repo access — ${var.project_name} ${var.environment}"
  type        = "SecureString"
  value       = var.github_access_token

  tags = {
    Name = "${var.project_name}-github-pat-${var.environment}"
  }

  lifecycle {
    ignore_changes = [value]
  }
}

resource "aws_iam_role_policy" "github_actions_deploy" {
  name = "${var.project_name}-github-deploy-${var.environment}"
  role = aws_iam_role.github_actions.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid      = "AmplifyDeploy"
        Effect   = "Allow"
        Action   = ["amplify:*"]
        Resource = "arn:aws:amplify:*:*:apps/*"
      },
      {
        Sid    = "ElasticBeanstalkDeploy"
        Effect = "Allow"
        Action = [
          "elasticbeanstalk:*",
          "ec2:Describe*",
          "autoscaling:*",
          "elasticloadbalancing:*",
          "s3:*",
          "cloudformation:*",
          "logs:*",
        ]
        Resource = "*"
      },
      {
        Sid      = "LambdaDeploy"
        Effect   = "Allow"
        Action   = ["lambda:*"]
        Resource = "arn:aws:lambda:*:*:function:${var.project_name}-*"
      },
      {
        Sid    = "LambdaEventSources"
        Effect = "Allow"
        Action = [
          "lambda:GetEventSourceMapping",
          "lambda:ListEventSourceMappings",
          "lambda:ListTags",
          "lambda:TagResource",
          "lambda:UntagResource",
          "lambda:CreateEventSourceMapping",
          "lambda:UpdateEventSourceMapping",
          "lambda:DeleteEventSourceMapping",
        ]
        Resource = "*"
      },
      {
        Sid    = "TerraformState"
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:PutObject",
          "s3:DeleteObject",
          "s3:ListBucket",
          "s3:GetBucketLocation",
        ]
        Resource = [
          aws_s3_bucket.tfstate.arn,
          "${aws_s3_bucket.tfstate.arn}/*",
        ]
      },
      {
        Sid    = "ReadPipelineSecrets"
        Effect = "Allow"
        Action = [
          "ssm:GetParameter",
          "ssm:GetParameters",
          "ssm:PutParameter",
          "ssm:DeleteParameter",
          "ssm:AddTagsToResource",
          "ssm:RemoveTagsFromResource",
          "ssm:ListTagsForResource",
        ]
        Resource = "arn:aws:ssm:*:*:parameter${local.ssm_prefix}/*"
      },
      {
        # ssm:DescribeParameters is a list-style action — AWS rejects it
        # with a scoped resource ARN, so it must be Resource = "*". Required
        # by Terraform during plan/refresh of any aws_ssm_parameter.
        Sid      = "DescribeAllSsmParameters"
        Effect   = "Allow"
        Action   = ["ssm:DescribeParameters"]
        Resource = "*"
      },
      {
        Sid    = "TerraformInfra"
        Effect = "Allow"
        Action = [
          "iam:GetRole",
          "iam:GetRolePolicy",
          "iam:GetInstanceProfile",
          "iam:GetOpenIDConnectProvider",
          "iam:ListRolePolicies",
          "iam:ListAttachedRolePolicies",
          "iam:ListInstanceProfilesForRole",
          "iam:PassRole",
          "iam:CreateRole",
          "iam:DeleteRole",
          "iam:AttachRolePolicy",
          "iam:DetachRolePolicy",
          "iam:PutRolePolicy",
          "iam:DeleteRolePolicy",
          "iam:CreateInstanceProfile",
          "iam:DeleteInstanceProfile",
          "iam:AddRoleToInstanceProfile",
          "iam:RemoveRoleFromInstanceProfile",
          "iam:TagRole",
          "iam:UntagRole",
          "iam:UpdateAssumeRolePolicy",
          "xray:*",
          "apigateway:*",
          "acm:*",
          "cognito-idp:*",
          "route53:*",
          "dynamodb:*",
          "rds:*",
          "cloudfront:*",
          "sqs:*",
          "sns:*",
          "events:*",
          "cloudwatch:*",
          "ec2:*",
          "kms:Describe*",
          "kms:List*",
          "kms:Get*",
        ]
        Resource = "*"
      }
    ]
  })
}
