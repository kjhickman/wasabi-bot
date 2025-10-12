output "ecr_repository_name" {
  description = "Name of the ECR repository that stores Wasabi Bot images."
  value       = aws_ecr_repository.wasabi_bot.name
}

output "ecr_repository_url" {
  description = "URL for pushing Wasabi Bot images to ECR."
  value       = aws_ecr_repository.wasabi_bot.repository_url
}
