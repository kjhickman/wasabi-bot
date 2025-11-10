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

output "ecs_service_name" {
  description = "Name of the ECS service running the Wasabi Bot API."
  value       = aws_ecs_service.wasabi_bot_api.name
}

output "ecs_service_arn" {
  description = "ARN of the ECS service running the Wasabi Bot API."
  value       = aws_ecs_service.wasabi_bot_api.arn
}

output "ecs_service_security_group_id" {
  description = "Security group attached to the Wasabi Bot API tasks."
  value       = aws_security_group.wasabi_bot_api.id
}

output "service_discovery_service_arn" {
  description = "Cloud Map service ARN for the Wasabi Bot API."
  value       = aws_service_discovery_service.wasabi_bot_api.arn
}

output "http_api_integration_id" {
  description = "Identifier of the shared HTTP API integration that fronts the Wasabi Bot service."
  value       = aws_apigatewayv2_integration.wasabi_bot_api.id
}

output "http_api_wasabi_base_url" {
  description = "Invoke URL for the Wasabi Bot service when accessed through its dedicated HTTP API."
  value       = trimsuffix(aws_apigatewayv2_stage.wasabi_bot.invoke_url, "/")
}
