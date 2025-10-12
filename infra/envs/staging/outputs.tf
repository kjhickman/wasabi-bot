output "ecr_repository_name" {
  description = "Name of the ECR repository that stores Wasabi Bot images."
  value       = aws_ecr_repository.wasabi_bot.name
}

output "ecr_repository_url" {
  description = "URL for pushing Wasabi Bot images to ECR."
  value       = aws_ecr_repository.wasabi_bot.repository_url
}

output "ecs_task_definition_arn" {
  description = "ARN of the Wasabi Bot API ECS task definition."
  value       = aws_ecs_task_definition.wasabi_bot_api.arn
}

output "ecs_task_definition_family" {
  description = "Family name of the Wasabi Bot API ECS task definition."
  value       = aws_ecs_task_definition.wasabi_bot_api.family
}

output "cloudwatch_log_group_name" {
  description = "CloudWatch Logs group used by the Wasabi Bot API task."
  value       = aws_cloudwatch_log_group.wasabi_bot_api.name
}
