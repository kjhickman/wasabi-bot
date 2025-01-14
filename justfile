# Default recipe that lists all available recipes
default:
    @just --list

# Start all services
up:
    docker compose up -d

# Stop and remove all containers
down:
    docker compose down

# Restart all services
restart:
    docker compose restart

# Build or rebuild services
build:
    docker compose build

# Start ngrok
ngrok:
    TARGET_HOST=host.docker.internal docker compose up -d ngrok

# Start postgres
postgres:
    docker compose up -d postgres

# Run migrations (requires postgres)
migrate: postgres
    docker compose up -d migrations

# Run migrations (requires postgres)
commands:
    docker compose up commands

# Start aspire
aspire:
    docker compose up -d aspire

# Start server in debug mode (requires multiple services)
debug: migrate ngrok postgres aspire