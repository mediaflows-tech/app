# infrastructure/modules/storage/main.tf

data "aws_caller_identity" "current" {}

locals {
  # S3 bucket names are globally unique. Suffixing with the account ID
  # keeps the module reusable against any future account and avoids
  # collisions with dead-account buckets that AWS hasn't released yet.
  bucket_name = "${var.project_name}-assets-${var.environment}-${data.aws_caller_identity.current.account_id}"
}

# ──────────────────────────────────────────────────
# S3 Bucket — Media Assets
# ──────────────────────────────────────────────────
resource "aws_s3_bucket" "media_assets" {
  bucket = local.bucket_name

  tags = {
    Name = local.bucket_name
  }

  lifecycle {
    prevent_destroy = false # Set to true in production after initial deployment
  }
}

resource "aws_s3_bucket_public_access_block" "media_assets" {
  bucket = aws_s3_bucket.media_assets.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_versioning" "media_assets" {
  bucket = aws_s3_bucket.media_assets.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "media_assets" {
  bucket = aws_s3_bucket.media_assets.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
    bucket_key_enabled = true
  }
}

resource "aws_s3_bucket_cors_configuration" "media_assets" {
  bucket = aws_s3_bucket.media_assets.id

  cors_rule {
    allowed_headers = ["*"]
    allowed_methods = ["GET", "HEAD", "PUT"]
    allowed_origins = var.cors_allowed_origins
    expose_headers  = ["ETag", "x-amz-request-id"]
    max_age_seconds = 3600
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "media_assets" {
  bucket = aws_s3_bucket.media_assets.id

  # Move quarantined content to Glacier after 30 days, delete after 90
  rule {
    id     = "quarantine-lifecycle"
    status = "Enabled"

    filter {
      prefix = "quarantine/"
    }

    transition {
      days          = 30
      storage_class = "GLACIER"
    }

    expiration {
      days = 90
    }
  }

  # Clean up incomplete multipart uploads after 7 days
  rule {
    id     = "abort-incomplete-multipart"
    status = "Enabled"

    filter {
      prefix = ""
    }

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }

  # Delete old non-current versions after 30 days
  rule {
    id     = "noncurrent-version-cleanup"
    status = "Enabled"

    filter {
      prefix = ""
    }

    noncurrent_version_expiration {
      noncurrent_days = 30
    }
  }
}
