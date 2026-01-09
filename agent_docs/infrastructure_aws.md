# AWS Infrastructure

Read this when working on deployment, infrastructure, or Terraform configurations.

## Overview

WasabiBot deploys to AWS using:

- **API Gateway** (HTTP API) — routes requests to ECS
- **ECS Fargate** — runs the .NET application as a container
- **ECR** — stores Docker images
- **CloudWatch** — logs and monitoring
- **ACM** — TLS certificates for custom domain

## Directory Structure

```
infra/
  envs/
    prod/       # Production environment
    staging/    # Staging environment
```

Each environment has:

- `main.tf` — resource definitions
- `variables.tf` — input variables
- `locals.tf` — local values (naming, tags)
- `outputs.tf` — exported values
- `providers.tf` — AWS provider configuration

## Key Resources (in main.tf)

| Resource | Purpose |
|----------|---------|
| `aws_apigatewayv2_api` | HTTP API gateway |
| `aws_apigatewayv2_stage` | API stage with logging and throttling |
| `aws_apigatewayv2_domain_name` | Custom domain configuration |
| `aws_ecr_repository` | Container image repository |
| `aws_cloudwatch_log_group` | API and Lambda logs |
| `aws_security_group` | Network security for Lambda |
| `aws_acm_certificate` | TLS certificate |

## Building the Docker Image

```bash
docker build -f src/WasabiBot.Api/Dockerfile -t wasabi-bot .
```

Default target is `linux-arm64`. Override with `--build-arg TARGET_RUNTIME=linux-x64`.

## Deploying

Terraform commands (from `infra/envs/<env>/`):

```bash
terraform init
terraform plan
terraform apply
```

## Environment Differences

- **Staging**: Lower throttling limits, shorter log retention
- **Production**: Higher limits, longer retention, production domain

## Checklist for Infrastructure Changes

- [ ] Make changes in both `staging/` and `prod/` if applicable
- [ ] Run `terraform plan` to preview changes
- [ ] Test in staging before applying to production
