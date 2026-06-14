# infrastructure/modules/dns/main.tf

terraform {
  required_providers {
    aws = {
      source                = "hashicorp/aws"
      configuration_aliases = [aws.us_east_1]
    }
  }
}

# ──────────────────────────────────────────────────
# Route53 Hosted Zone
# ──────────────────────────────────────────────────
resource "aws_route53_zone" "main" {
  name = var.domain_name

  tags = {
    Name = "${var.project_name}-zone-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# ACM Certificate — Regional (ap-southeast-1, for ALB)
# ──────────────────────────────────────────────────
resource "aws_acm_certificate" "regional" {
  domain_name               = var.domain_name
  subject_alternative_names = ["*.${var.domain_name}"]
  validation_method         = "DNS"

  tags = {
    Name = "${var.project_name}-cert-regional-${var.environment}"
  }

  lifecycle {
    create_before_destroy = true
  }
}

# ──────────────────────────────────────────────────
# ACM Certificate — CloudFront (us-east-1, for CloudFront + Cognito)
# ──────────────────────────────────────────────────
resource "aws_acm_certificate" "cloudfront" {
  provider                  = aws.us_east_1
  domain_name               = var.domain_name
  subject_alternative_names = ["*.${var.domain_name}"]
  validation_method         = "DNS"

  tags = {
    Name = "${var.project_name}-cert-cloudfront-${var.environment}"
  }

  lifecycle {
    create_before_destroy = true
  }
}

# ──────────────────────────────────────────────────
# DNS Validation Records (shared by both certs — same domain)
# ──────────────────────────────────────────────────
locals {
  # Both certs cover the same domains, so validation records are identical.
  # Use the regional cert's options as the canonical source.
  validation_options = {
    for dvo in aws_acm_certificate.regional.domain_validation_options : dvo.domain_name => {
      name   = dvo.resource_record_name
      record = dvo.resource_record_value
      type   = dvo.resource_record_type
    }
  }
}

resource "aws_route53_record" "cert_validation" {
  for_each = local.validation_options

  allow_overwrite = true
  name            = each.value.name
  records         = [each.value.record]
  ttl             = 60
  type            = each.value.type
  zone_id         = aws_route53_zone.main.zone_id
}

resource "aws_acm_certificate_validation" "regional" {
  certificate_arn         = aws_acm_certificate.regional.arn
  validation_record_fqdns = [for record in aws_route53_record.cert_validation : record.fqdn]
}

resource "aws_acm_certificate_validation" "cloudfront" {
  provider                = aws.us_east_1
  certificate_arn         = aws_acm_certificate.cloudfront.arn
  validation_record_fqdns = [for record in aws_route53_record.cert_validation : record.fqdn]
}

# ──────────────────────────────────────────────────
# www redirect: S3 bucket + CloudFront
# ──────────────────────────────────────────────────
data "aws_caller_identity" "current" {}

locals {
  # S3 bucket names are globally unique; suffix with account ID.
  www_redirect_bucket_name = "${var.project_name}-www-redirect-${var.environment}-${data.aws_caller_identity.current.account_id}"
}

resource "aws_s3_bucket" "www_redirect" {
  count  = var.manage_www_redirect ? 1 : 0
  bucket = local.www_redirect_bucket_name

  tags = {
    Name = local.www_redirect_bucket_name
  }
}

resource "aws_s3_bucket_public_access_block" "www_redirect" {
  count  = var.manage_www_redirect ? 1 : 0
  bucket = aws_s3_bucket.www_redirect[0].id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_website_configuration" "www_redirect" {
  count  = var.manage_www_redirect ? 1 : 0
  bucket = aws_s3_bucket.www_redirect[0].id

  redirect_all_requests_to {
    host_name = var.domain_name
    protocol  = "https"
  }
}

resource "aws_cloudfront_distribution" "www_redirect" {
  count       = var.manage_www_redirect ? 1 : 0
  enabled     = true
  comment     = "www to root redirect - ${var.environment}"
  aliases     = ["www.${var.domain_name}"]
  price_class = "PriceClass_100"

  origin {
    domain_name = aws_s3_bucket_website_configuration.www_redirect[0].website_endpoint
    origin_id   = "S3-www-redirect"

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "http-only"
      origin_ssl_protocols   = ["TLSv1.2"]
    }
  }

  default_cache_behavior {
    allowed_methods        = ["GET", "HEAD"]
    cached_methods         = ["GET", "HEAD"]
    target_origin_id       = "S3-www-redirect"
    viewer_protocol_policy = "redirect-to-https"

    forwarded_values {
      query_string = false
      cookies {
        forward = "none"
      }
    }
  }

  viewer_certificate {
    acm_certificate_arn      = aws_acm_certificate_validation.cloudfront.certificate_arn
    ssl_support_method       = "sni-only"
    minimum_protocol_version = "TLSv1.2_2021"
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  tags = {
    Name = "${var.project_name}-www-redirect-cdn-${var.environment}"
  }
}

resource "aws_route53_record" "www" {
  count   = var.manage_www_redirect ? 1 : 0
  zone_id = aws_route53_zone.main.zone_id
  name    = "www.${var.domain_name}"
  type    = "A"

  alias {
    name                   = aws_cloudfront_distribution.www_redirect[0].domain_name
    zone_id                = aws_cloudfront_distribution.www_redirect[0].hosted_zone_id
    evaluate_target_health = false
  }
}
