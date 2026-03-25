# AWS to Fly.io Migration Plan

## Goals

- Move application hosting from AWS to Fly.io.
- Move PostgreSQL hosting from Neon to a single self-managed Postgres instance on Fly.io.
- Use one Fly Postgres instance with two logical databases:
  - `wasabi_bot_prod`
  - `wasabi_bot_staging`
- Keep separate prod and staging application deployments.
- Do not migrate existing data from Neon.
- Allow some downtime during cutover.
- Move public URLs from AWS to Fly.io, including `wasabibot.com` and `staging.wasabibot.com`.

## Current State

The repo currently deploys a single ARM64 .NET app container to AWS ECS/Fargate behind API Gateway, with separate staging and prod Terraform environments.

Relevant repo details:

- App container listens on port `8080`: `src/WasabiBot.Api/Dockerfile`
- Health endpoints exist at `/health` and `/alive`: `src/WasabiBot.ServiceDefaults/Extensions.cs`
- App reads DB connection string from configuration: `src/WasabiBot.Api/Infrastructure/Database/DependencyInjection.cs`
- Migration runner applies EF Core migrations against whichever connection string it receives: `src/WasabiBot.Migrations/MigrationRunner.cs`
- Prod domain is `wasabibot.com`: `infra/envs/prod/locals.tf`
- Staging domain is `staging.wasabibot.com`: `infra/envs/staging/locals.tf`
- Current AWS deploy workflow builds Docker image, applies Terraform, and runs migrations: `.github/workflows/deploy.yml`

Current environment separation is done through different connection strings, not through schema-aware code. That makes two logical databases on one Fly Postgres instance the simplest and safest migration target.

## Recommended Target Architecture

### Applications

- One Fly app for production
- One Fly app for staging
- Both deploy from the existing Dockerfile
- Both run a single machine/instance initially

Single-instance deployment is recommended at first because the app includes an in-process reminder background worker. Scaling horizontally before redesigning that worker could create duplicate processing behavior.

### Database

- One self-managed Postgres instance on Fly.io
- Two logical databases on that instance:
  - `wasabi_bot_prod`
  - `wasabi_bot_staging`
- Separate DB users and credentials for each app if Fly makes that convenient; otherwise separate connection strings at minimum

### Secrets and Config

Each Fly app should have its own secrets/config for:

- `ConnectionStrings__wasabi_db`
- `Discord__Token`
- `Authentication__Discord__ClientId`
- `Authentication__Discord__ClientSecret`
- `Authentication__Token__SigningKey`
- `Gemini__ApiKey`
- `Grok__ApiKey`
- `OTEL_EXPORTER_OTLP_HEADERS`

The non-secret OTEL endpoint/protocol values can stay as environment variables or config values.

## Why 2 Databases on 1 Fly Postgres Instance

This is the best fit for the current codebase because:

- It preserves the current app pattern of environment separation via connection string.
- It avoids adding EF Core schema-selection logic.
- It avoids schema-specific migration complexity.
- It keeps cost down by sharing one Postgres instance across both environments.
- It provides cleaner isolation between prod and staging than shared schemas.

Using one database with two schemas is possible, but it would add avoidable migration and maintenance complexity for this project.

## Migration Strategy

We will treat this as a fresh infrastructure migration, not a data migration.

- No Neon data import.
- Fresh databases on the Fly-hosted Postgres instance.
- Fresh migrations applied into each Fly-hosted database.
- Traffic cut over from AWS to Fly.io after validation.

## Phase 1: Prepare Fly.io App Deployment

### Tasks

- Create Fly app config for staging.
- Create Fly app config for prod.
- Configure internal port `8080`.
- Configure Fly health checks to use `/health`.
- Ensure deployment uses the existing Dockerfile.
- Keep machine count at `1` for each environment initially.
- Add GitHub Actions workflows for Fly deployments.
- During migration, allow Fly staging deployment automation from both `main` and `flyio` so the migration branch can be exercised before merge.
- Configure Fly GitHub Actions workflows to run migrations before `flyctl deploy`.

### Expected Repo Changes

- Add Fly config file(s), likely under `src/WasabiBot.Api/`, such as `src/WasabiBot.Api/fly.prod.toml` and `src/WasabiBot.Api/fly.staging.toml`.
- Add or replace CI/CD workflow steps so deployments go to Fly instead of AWS.
- Remove AWS-specific deployment assumptions from the pipeline only after Fly is working.

### Branching Note

Because migration work is happening on branch `flyio`, staging automation should temporarily support pushes from both `main` and `flyio`.

Recommended behavior:

- Fly staging deploys run from `main` and `flyio`
- Fly prod deploys remain manual
- Existing AWS deploy workflows should not be expanded to `flyio` unless we explicitly want to keep deploying the migration branch to AWS too

## Phase 2: Provision Postgres on Fly

### Tasks

- Create one Postgres instance on Fly in the chosen region.
- Create database `wasabi_bot_prod`.
- Create database `wasabi_bot_staging`.
- Generate separate connection strings for prod and staging.
- Store each connection string in the correct Fly app secrets.

### Notes

- Since no data migration is needed, there is no need for dump/restore from Neon.
- Each environment should run migrations independently against its own logical database.

## Phase 3: Migrate Secrets and Environment Configuration

### Tasks

- Inventory secrets currently sourced from AWS SSM.
- Load equivalent secrets into Fly for staging.
- Load equivalent secrets into Fly for prod.
- Verify all application configuration values resolve correctly from environment variables and Fly secrets.

### Important App Consideration

`src/WasabiBot.Api/Program.cs` no longer depends on AWS Systems Manager, so Fly-hosted environments can rely on normal environment variables and Fly secrets.

## Phase 4: Deploy and Validate Staging on Fly.io

### Tasks

- Deploy staging app to Fly.
- Point staging app at `wasabi_bot_staging`.
- Run EF Core migrations against `wasabi_bot_staging`.
- Validate health endpoint.
- Validate Discord auth flows and any callback URLs.
- Validate reminder behavior.
- Validate telemetry/logging.

### Success Criteria

- App starts successfully on Fly.
- `/health` returns healthy.
- Staging login/auth and command flows work.
- Staging database tables are created correctly.

## Phase 5: Deploy and Validate Production on Fly.io

### Tasks

- Deploy prod app to Fly.
- Point prod app at `wasabi_bot_prod`.
- Run EF Core migrations against `wasabi_bot_prod`.
- Verify application behavior using the Fly hostname before DNS cutover.
- Confirm prod secrets and Discord configuration are correct.

### Success Criteria

- Prod app is healthy on Fly before any DNS changes.
- Production database is initialized and reachable.
- Smoke tests pass on the Fly URL.

## Phase 6: DNS and URL Cutover

### Domains

- `wasabibot.com`
- `staging.wasabibot.com`

### Tasks

- Add both domains/certificates to the appropriate Fly apps.
- Lower DNS TTL in advance, if possible.
- Confirm Fly certificates are issued and healthy.
- Schedule a maintenance window.
- Stop or scale down AWS workloads during cutover.
- Update DNS records to point the domains at Fly.io.
- Verify HTTPS and routing after propagation.

### Expected Downtime

Some downtime is acceptable, so the simplest cutover is:

1. Put the app in maintenance mode or stop AWS app traffic.
2. Confirm Fly app is healthy.
3. Change DNS to Fly.
4. Wait for propagation.
5. Verify live traffic on Fly.

This is lower risk than trying to run AWS and Fly in parallel on the same production domain for a hobby deployment.

## Phase 7: Post-Cutover Validation

### Checks

- `wasabibot.com` resolves to Fly and serves the app correctly.
- `staging.wasabibot.com` resolves to Fly and serves the app correctly.
- TLS certificates are valid.
- Production auth works.
- Discord integration works.
- Background reminder processing still behaves correctly with one instance.
- Database connections are stable.
- Observability/logging still works as expected.

## Phase 8: Decommission Old Infrastructure

Only do this after Fly has been stable for a short observation period.

### Tasks

- Remove Neon dependency and secrets.
- Remove AWS deployment workflow pieces.
- Tear down AWS ECS/API Gateway/ECR/Terraform-managed app resources no longer needed.
- Remove unused AWS SSM parameters related to runtime hosting, if desired.
- Keep a backup of the old infrastructure definitions in git history.

## Rollback Plan

Because no data is being migrated from Neon, rollback is operationally simple during the cutover window.

If Fly has issues before or during DNS cutover:

- Keep AWS serving traffic.
- Fix Fly deployment and retry later.

If Fly has issues just after DNS cutover:

- Repoint DNS back to AWS.
- Restart AWS app if it was scaled down.
- Investigate Fly issues before attempting another cutover.

This rollback is much easier because there is no cross-provider production data sync requirement.

## Main Risks

- Fly deployment config may need iteration during first deployment.
- Running Postgres yourself on Fly means backups, upgrades, and failover are your responsibility.
- Discord auth/callback configuration may need updates for Fly-hosted URLs.
- The reminder processor should remain single-instance until redesigned for distributed coordination.
- DNS cutover can take time depending on TTL and DNS provider behavior.

## Recommended Order of Work

1. Add Fly deployment config.
2. Configure Fly environment variables and secrets for hosted environments.
3. Create one Fly Postgres instance with two logical databases.
4. Move staging to Fly and validate it.
5. Move prod to Fly and validate it on the Fly hostname.
6. Cut DNS for `staging.wasabibot.com`.
7. Cut DNS for `wasabibot.com`.
8. Observe stability.
9. Decommission AWS and Neon.

## Concrete Deliverables

- Fly app configuration for prod and staging
- Fly deployment workflow in GitHub Actions
- One Fly-hosted Postgres instance
- Two logical databases on that Postgres instance
- Fly secrets for both environments
- Updated runtime config to stop depending on AWS SSM in Fly
- DNS updates for `wasabibot.com` and `staging.wasabibot.com`
- AWS and Neon decommission checklist

## Summary Decision

Chosen approach:

- Hosting: Fly.io
- Database host: self-managed Postgres on Fly.io
- Database topology: one Fly Postgres instance with two logical databases
- Environment model: separate staging and prod apps, separate connection strings, separate logical databases
- Data migration: none
- Cutover style: short maintenance window with DNS switch
