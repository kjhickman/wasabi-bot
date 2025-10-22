data "aws_region" "current" {}

data "aws_apigatewayv2_api" "shared_http_api" {
  api_id = local.http_api_id
}

resource "aws_ecr_repository" "wasabi_bot" {
  name                 = "${local.project}/${local.api_service_name}-${local.environment}"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "aws_ecr_lifecycle_policy" "wasabi_bot" {
  repository = aws_ecr_repository.wasabi_bot.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep the 100 most recent images"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = 100
        }
        action = {
          type = "expire"
        }
      }
    ]
  })
}

resource "aws_cloudwatch_log_group" "wasabi_bot_api" {
  name              = local.api_log_group_name
  retention_in_days = 14
}

resource "aws_security_group" "wasabi_bot_api" {
  name        = "${local.api_container_name}-sg"
  description = "Security group for the Wasabi Bot API service."
  vpc_id      = local.vpc_id

  ingress {
    description     = "Allow API Gateway VPC Link traffic"
    from_port       = 8080
    to_port         = 8080
    protocol        = "tcp"
    security_groups = [local.vpc_link_security_group_id]
  }

  egress {
    description = "Allow outbound traffic"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "${local.api_container_name}-sg"
  }
}

resource "aws_service_discovery_service" "wasabi_bot_api" {
  name = "${local.api_container_name}-http"
  force_destroy = true

  dns_config {
    namespace_id   = local.service_discovery_namespace_id
    routing_policy = "MULTIVALUE"

    dns_records {
      ttl  = 10
      type = "SRV"
    }
  }

  # No custom health check; ECS handles container status.
}

resource "aws_ecs_task_definition" "wasabi_bot_api" {
  family                   = local.api_container_name
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = local.api_task_execution_role_arn
  task_role_arn            = local.api_task_role_arn

  container_definitions = jsonencode([
    {
      name      = local.api_container_name
      image     = "${aws_ecr_repository.wasabi_bot.repository_url}@${var.image_digest}"
      essential = true
      portMappings = [
        {
          containerPort = 8080
          hostPort      = 8080
          protocol      = "tcp"
        }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.wasabi_bot_api.name
          awslogs-region        = data.aws_region.current.id
          awslogs-stream-prefix = "ecs"
        }
      }
      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = title(local.environment)
        },
        {
          name  = "ASPNETCORE_PATHBASE"
          value = local.http_api_path_base
        }
      ]
      secrets = [
        {
          name      = "Gemini__ApiKey"
          valueFrom = "/wasabi-bot/shared/GeminiApiKey"
        },
        {
          name      = "Discord__Token"
          valueFrom = "/wasabi-bot/${local.environment}/DiscordToken"
        },
        {
          name      = "ConnectionStrings__wasabi-db"
          valueFrom = "/wasabi-bot/${local.environment}/NeonDbConnectionString"
        }
      ]
    }
  ])

  runtime_platform {
    operating_system_family = "LINUX"
    cpu_architecture        = "ARM64"
  }

  tags = {
    Name = local.api_container_name
  }
}

resource "aws_ecs_service" "wasabi_bot_api" {
  name                              = local.api_container_name
  cluster                           = local.ecs_cluster_name
  task_definition                   = aws_ecs_task_definition.wasabi_bot_api.arn
  desired_count                     = 1
  health_check_grace_period_seconds = 60

  deployment_minimum_healthy_percent = 50
  deployment_maximum_percent         = 200
  enable_execute_command             = true

  network_configuration {
    subnets          = local.public_subnet_ids
    security_groups  = [aws_security_group.wasabi_bot_api.id]
    assign_public_ip = true
  }

  service_registries {
    registry_arn = aws_service_discovery_service.wasabi_bot_api.arn
    port         = 8080
  }

  depends_on = [
    aws_cloudwatch_log_group.wasabi_bot_api
  ]
}

resource "aws_apigatewayv2_integration" "wasabi_bot_api" {
  api_id                 = data.aws_apigatewayv2_api.shared_http_api.id
  integration_type       = "HTTP_PROXY"
  integration_method     = "ANY"
  integration_uri        = aws_service_discovery_service.wasabi_bot_api.arn
  connection_type        = "VPC_LINK"
  connection_id          = local.http_api_vpc_link_id
  payload_format_version = "1.0"
  timeout_milliseconds   = 29000
  description            = "Routes Wasabi Bot API traffic through the shared gateway."
}

resource "aws_apigatewayv2_route" "wasabi_bot_api_root" {
  api_id    = data.aws_apigatewayv2_api.shared_http_api.id
  route_key = "ANY /wasabi"
  target    = "integrations/${aws_apigatewayv2_integration.wasabi_bot_api.id}"
}

resource "aws_apigatewayv2_route" "wasabi_bot_api_proxy" {
  api_id    = data.aws_apigatewayv2_api.shared_http_api.id
  route_key = "ANY /wasabi/{proxy+}"
  target    = "integrations/${aws_apigatewayv2_integration.wasabi_bot_api.id}"
}

resource "neon_role" "main" {
  project_id = local.neon_project_id
  branch_id  = local.neon_project_default_branch_id
  name       = "${local.project}_admin"
}

resource "neon_database" "main" {
  project_id = local.neon_project_id
  branch_id  = local.neon_project_default_branch_id
  name       = local.project
  owner_name = neon_role.main.name
}
