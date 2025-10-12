data "aws_region" "current" {}

resource "aws_ecr_repository" "wasabi_bot" {
  name                 = "${local.project}/${local.api_service_name}-${local.environment}"
  image_tag_mutability = "IMMUTABLE"

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
        description  = "Keep the 20 most recent images"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = 20
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
        }
      ]
      secrets = [
        {
          name      = "Gemini__ApiKey"
          valueFrom = "/wasabi-bot/shared/GeminiApiKey"
        },
        {
          name      = "Discord__Token"
          valueFrom = "/wasabi-bot/staging/DiscordToken"
        }
      ]
    }
  ])

  runtime_platform {
    operating_system_family = "LINUX"
    cpu_architecture        = "X86_64"
  }

  tags = {
    Name = local.api_container_name
  }
}
