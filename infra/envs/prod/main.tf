data "aws_region" "current" {}

resource "aws_cloudwatch_log_group" "http_api" {
  name              = "/aws/apigateway/http/${local.project}/${local.environment}"
  retention_in_days = 30
}

resource "aws_acm_certificate" "wasabi_bot" {
  domain_name               = local.domain_name
  subject_alternative_names = []
  validation_method         = "DNS"

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_apigatewayv2_domain_name" "wasabi_bot" {
  domain_name = local.domain_name

  domain_name_configuration {
    certificate_arn = aws_acm_certificate.wasabi_bot.arn
    endpoint_type   = "REGIONAL"
    security_policy = "TLS_1_2"
  }
}

resource "aws_apigatewayv2_api" "wasabi_bot" {
  name          = "${local.project}-${local.environment}-http-api"
  protocol_type = "HTTP"
}

resource "aws_apigatewayv2_stage" "wasabi_bot" {
  api_id      = aws_apigatewayv2_api.wasabi_bot.id
  name        = local.http_api_stage_name
  auto_deploy = true

  access_log_settings {
    destination_arn = aws_cloudwatch_log_group.http_api.arn
    format = jsonencode({
      requestId               = "$context.requestId"
      sourceIp                = "$context.identity.sourceIp"
      requestTime             = "$context.requestTime"
      httpMethod              = "$context.httpMethod"
      routeKey                = "$context.routeKey"
      status                  = "$context.status"
      protocol                = "$context.protocol"
      responseLength          = "$context.responseLength"
      integrationStatus       = "$context.integrationStatus"
      integrationErrorMessage = "$context.integrationErrorMessage"
    })
  }

  default_route_settings {
    throttling_burst_limit = 1000
    throttling_rate_limit  = 500
  }
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
  name          = "${local.api_container_name}-http"
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
      healthCheck = {
        command     = ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      }
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
        }
      ]
      secrets = [
        {
          name      = "Gemini__ApiKey"
          valueFrom = "/wasabi-bot/shared/GeminiApiKey"
        },
        {
          name      = "Grok__ApiKey"
          valueFrom = "/wasabi-bot/shared/GrokApiKey"
        },
        {
          name      = "Discord__Token"
          valueFrom = "/wasabi-bot/${local.environment}/DiscordToken"
        },
        {
          name      = "Authentication__Discord__ClientId"
          valueFrom = "/wasabi-bot/${local.environment}/DiscordClientId"
        },
        {
          name      = "Authentication__Discord__ClientSecret"
          valueFrom = "/wasabi-bot/${local.environment}/DiscordClientSecret"
        },
        {
          name      = "Authentication__Token__SigningKey"
          valueFrom = "/wasabi-bot/${local.environment}/TokenSigningKey"
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
  name                               = local.api_container_name
  cluster                            = local.ecs_cluster_name
  task_definition                    = aws_ecs_task_definition.wasabi_bot_api.arn
  desired_count                      = 1
  health_check_grace_period_seconds  = 60
  force_new_deployment               = true
  deployment_minimum_healthy_percent = 50
  deployment_maximum_percent         = 200
  enable_execute_command             = true

  capacity_provider_strategy {
    capacity_provider = "FARGATE_SPOT"
    weight            = 1
  }

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
  api_id                 = aws_apigatewayv2_api.wasabi_bot.id
  integration_type       = "HTTP_PROXY"
  integration_method     = "ANY"
  integration_uri        = aws_service_discovery_service.wasabi_bot_api.arn
  connection_type        = "VPC_LINK"
  connection_id          = local.vpc_link_id
  payload_format_version = "1.0"
  timeout_milliseconds   = 29000
  description            = "Routes Wasabi Bot API traffic through the dedicated gateway."
}

resource "aws_apigatewayv2_route" "wasabi_bot_api_root" {
  api_id    = aws_apigatewayv2_api.wasabi_bot.id
  route_key = "ANY /"
  target    = "integrations/${aws_apigatewayv2_integration.wasabi_bot_api.id}"
}

resource "aws_apigatewayv2_route" "wasabi_bot_api_proxy" {
  api_id    = aws_apigatewayv2_api.wasabi_bot.id
  route_key = "ANY /{proxy+}"
  target    = "integrations/${aws_apigatewayv2_integration.wasabi_bot_api.id}"
}

resource "aws_apigatewayv2_api_mapping" "wasabi_bot" {
  api_id          = aws_apigatewayv2_api.wasabi_bot.id
  domain_name     = aws_apigatewayv2_domain_name.wasabi_bot.id
  stage           = aws_apigatewayv2_stage.wasabi_bot.name

  depends_on = [
    aws_apigatewayv2_route.wasabi_bot_api_root,
    aws_apigatewayv2_route.wasabi_bot_api_proxy
  ]
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
