terraform {
  required_providers {
    fly = {
      source  = "andrewbaxter/fly"
      version = "~> 0.1"
    }
  }

  backend "pg" {
    conn_str = "postgresql://<username>:<password>@<host>:<port>/<dbname>?sslmode=disable"
    schema_name = "public"
    table_name  = "{env}_terraform_states"
  }
}

provider "fly" {
  # API token for authentication
  access_token = var.fly_api_token
}