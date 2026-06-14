# infrastructure/modules/networking/main.tf

# ──────────────────────────────────────────────────
# VPC
# ──────────────────────────────────────────────────
resource "aws_vpc" "main" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = {
    Name = "${var.project_name}-vpc-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# Internet Gateway
# ──────────────────────────────────────────────────
resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${var.project_name}-igw-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# Public Subnets (ALB, NAT Gateway)
# ──────────────────────────────────────────────────
resource "aws_subnet" "public" {
  count                   = 2
  vpc_id                  = aws_vpc.main.id
  cidr_block              = cidrsubnet(var.vpc_cidr, 8, count.index + 1) # 10.0.1.0/24, 10.0.2.0/24
  availability_zone       = var.availability_zones[count.index]
  map_public_ip_on_launch = true

  tags = {
    Name = "${var.project_name}-public-${count.index + 1}-${var.environment}"
    Tier = "Public"
  }
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }

  tags = {
    Name = "${var.project_name}-public-rt-${var.environment}"
  }
}

resource "aws_route_table_association" "public" {
  count          = 2
  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

# ──────────────────────────────────────────────────
# Private App Subnets (Elastic Beanstalk EC2 instances)
# ──────────────────────────────────────────────────
resource "aws_subnet" "private_app" {
  count             = 2
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 8, count.index + 10) # 10.0.10.0/24, 10.0.11.0/24
  availability_zone = var.availability_zones[count.index]

  tags = {
    Name = "${var.project_name}-private-app-${count.index + 1}-${var.environment}"
    Tier = "PrivateApp"
  }
}

# ──────────────────────────────────────────────────
# Private DB Subnets (RDS PostgreSQL)
# ──────────────────────────────────────────────────
resource "aws_subnet" "private_db" {
  count             = 2
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 8, count.index + 20) # 10.0.20.0/24, 10.0.21.0/24
  availability_zone = var.availability_zones[count.index]

  tags = {
    Name = "${var.project_name}-private-db-${count.index + 1}-${var.environment}"
    Tier = "PrivateDB"
  }
}

# ──────────────────────────────────────────────────
# NAT Gateway (single NAT for cost savings in dev)
# ──────────────────────────────────────────────────

moved {
  from = aws_eip.nat
  to   = aws_eip.nat[0]
}

moved {
  from = aws_nat_gateway.main
  to   = aws_nat_gateway.main[0]
}

resource "aws_eip" "nat" {
  count  = var.nat_enabled ? 1 : 0
  domain = "vpc"

  tags = {
    Name = "${var.project_name}-nat-eip-${var.environment}"
  }
}

resource "aws_nat_gateway" "main" {
  count         = var.nat_enabled ? 1 : 0
  allocation_id = aws_eip.nat[0].id
  subnet_id     = aws_subnet.public[0].id

  tags = {
    Name = "${var.project_name}-nat-${var.environment}"
  }

  depends_on = [aws_internet_gateway.main]
}

resource "aws_route_table" "private" {
  vpc_id = aws_vpc.main.id

  # NAT route is defined as a separate aws_route resource so it can be
  # conditionally created/destroyed via the services_enabled flag without
  # destroying the route table itself (which would also destroy the
  # route_table_associations — unnecessary churn).

  tags = {
    Name = "${var.project_name}-private-rt-${var.environment}"
  }
}

resource "aws_route" "private_nat" {
  count                  = var.nat_enabled ? 1 : 0
  route_table_id         = aws_route_table.private.id
  destination_cidr_block = "0.0.0.0/0"
  nat_gateway_id         = aws_nat_gateway.main[0].id
}

resource "aws_route_table_association" "private_app" {
  count          = 2
  subnet_id      = aws_subnet.private_app[count.index].id
  route_table_id = aws_route_table.private.id
}

resource "aws_route_table_association" "private_db" {
  count          = 2
  subnet_id      = aws_subnet.private_db[count.index].id
  route_table_id = aws_route_table.private.id
}

# ──────────────────────────────────────────────────
# S3 Gateway VPC Endpoint (free — avoids NAT costs for S3)
# ──────────────────────────────────────────────────
resource "aws_vpc_endpoint" "s3" {
  vpc_id       = aws_vpc.main.id
  service_name = "com.amazonaws.ap-southeast-1.s3"

  route_table_ids = [
    aws_route_table.private.id,
  ]

  tags = {
    Name = "${var.project_name}-s3-endpoint-${var.environment}"
  }
}

# ──────────────────────────────────────────────────
# Security Groups
# ──────────────────────────────────────────────────

# ALB Security Group — accepts HTTPS from anywhere
resource "aws_security_group" "alb" {
  name_prefix = "${var.project_name}-alb-${var.environment}-"
  description = "Security group for Application Load Balancer"
  vpc_id      = aws_vpc.main.id

  ingress {
    description = "HTTPS from anywhere"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "HTTP from anywhere (redirects to HTTPS)"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    description = "All outbound"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "${var.project_name}-alb-sg-${var.environment}"
  }

  lifecycle {
    create_before_destroy = true
  }
}

# App Security Group — accepts traffic from ALB only
resource "aws_security_group" "app" {
  name_prefix = "${var.project_name}-app-${var.environment}-"
  description = "Security group for application instances"
  vpc_id      = aws_vpc.main.id

  ingress {
    description     = "HTTP from ALB"
    from_port       = 5000
    to_port         = 5000
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

  egress {
    description = "All outbound"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "${var.project_name}-app-sg-${var.environment}"
  }

  lifecycle {
    create_before_destroy = true
  }
}

# DB Security Group — accepts traffic from App and Lambda only.
#
# Ingress drift is ignored because we also manage ingress via the
# standalone aws_security_group_rule.db_from_lambda resource (below).
# The AWS provider warns against mixing inline ingress blocks with
# standalone rules — they fight each other on every plan ("inline says
# 1 rule, AWS has 2, plan to remove 1"). ignore_changes = [ingress]
# tells Terraform to stop reconciling the inline-vs-standalone
# difference on subsequent applies — the inline block still creates
# the App ingress at first apply, then drift is tolerated.
resource "aws_security_group" "db" {
  name_prefix = "${var.project_name}-db-${var.environment}-"
  description = "Security group for RDS PostgreSQL"
  vpc_id      = aws_vpc.main.id

  ingress {
    description     = "PostgreSQL from App instances"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.app.id]
  }

  egress {
    description = "All outbound"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "${var.project_name}-db-sg-${var.environment}"
  }

  lifecycle {
    create_before_destroy = true
    ignore_changes        = [ingress]
  }
}

# Lambda Security Group — for VPC-attached Lambda functions
resource "aws_security_group" "lambda" {
  name_prefix = "${var.project_name}-lambda-${var.environment}-"
  description = "Security group for Lambda functions in VPC"
  vpc_id      = aws_vpc.main.id

  egress {
    description = "All outbound"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "${var.project_name}-lambda-sg-${var.environment}"
  }

  lifecycle {
    create_before_destroy = true
  }
}

# Allow Lambda to access DB
resource "aws_security_group_rule" "db_from_lambda" {
  type                     = "ingress"
  from_port                = 5432
  to_port                  = 5432
  protocol                 = "tcp"
  source_security_group_id = aws_security_group.lambda.id
  security_group_id        = aws_security_group.db.id
  description              = "PostgreSQL from Lambda functions"
}
