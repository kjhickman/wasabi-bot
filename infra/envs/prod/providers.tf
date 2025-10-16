terraform {
  required_version = ">= 1.13.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }

    neon = {
      source  = "kislerdm/neon"
      version = ">= 0.10.0"
    }
  }

  backend "s3" {
    bucket       = "tfstate-346610971261"
    key          = "wasabi-bot-prod.tfstate"
    region       = "us-east-1"
    use_lockfile = true
    encrypt      = true
  }
}

provider "aws" {
  region = "us-east-1"

  default_tags {
    tags = {
      Environment = "prod"
      Project     = "wasabi-bot"
      ManagedBy   = "terraform"
    }
  }
}

# API key is read from the environment variable `NEON_API_KEY`
provider "neon" {}
