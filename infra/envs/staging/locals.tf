data "terraform_remote_state" "network" {
  backend = "s3"

  config = {
    bucket = "tfstate-346610971261"
    key    = "infra-${local.environment}.tfstate"
    region = "us-east-1"
  }
}

locals {
  environment                            = "staging"
  project                                = "wasabi-bot"
  api_service_name                       = "wasabi-bot-api"
  api_container_name                     = "${local.api_service_name}-${local.environment}"
  api_log_group_name                     = "/aws/ecs/${local.api_container_name}"
  api_task_execution_role_arn            = data.terraform_remote_state.network.outputs.ecs_task_execution_role_arn
  api_task_role_arn                      = data.terraform_remote_state.network.outputs.ecs_task_role_arn
  ecs_cluster_name                       = data.terraform_remote_state.network.outputs.ecs_cluster_name
  vpc_id                                 = data.terraform_remote_state.network.outputs.vpc_id
  public_subnet_ids                      = data.terraform_remote_state.network.outputs.public_subnet_ids
  vpc_link_id                            = data.terraform_remote_state.network.outputs.vpc_link_id
  vpc_link_security_group_id             = data.terraform_remote_state.network.outputs.vpc_link_security_group_id
  service_discovery_namespace_id         = data.terraform_remote_state.network.outputs.service_discovery_namespace_id
  dsql_cluster_arn                       = data.terraform_remote_state.network.outputs.dsql_cluster_arn
  dsql_cluster_vpc_endpoint_service_name = data.terraform_remote_state.network.outputs.dsql_cluster_vpc_endpoint_service_name
}
