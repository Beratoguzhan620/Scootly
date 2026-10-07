# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Optional field-task photo upload in Mvc (JPEG/PNG up to 5 MB, content-sniffed, S3-compatible storage via SeaweedFS)
  with short-lived pre-signed download links (ADR 0043).
- Account endpoints: `GET /api/account`, `POST /api/account/change-password` (returns a fresh token) and
  `DELETE /api/account` (password-confirmed; ride locations are anonymized immediately).
- Driver ride history: `GET /api/rides` (paged, newest first).
- Vehicles can be marked lost (`POST /api/v1/vehicles/{id}/lost`) and returned to service; Mvc vehicle list gained
  "send to maintenance", "mark lost" and "return to service" actions for fleet staff.
- v2 vehicle list accepts the same bounding-box and availability filters as v1.
- Battery reconciliation: the battery scanner opens missing battery-replacement tasks (with a 6-hour cooldown after a
  completed task).
- New test projects `Scootly.Worker.Tests` and `Scootly.Mvc.IntegrationTests`; architecture rules now cover Mvc.
- CI: gitleaks secret scan, `dotnet format` whitespace check, Trivy image scan; Dependabot for NuGet, Actions and Docker.
- `db-init` compose service that creates a least-privilege application database role (`scootly_app`).
- Blue-green deployment skeleton (nginx include, green compose file, 2 replicas) and incident runbook.

### Changed
- Service-area boundaries are stored as a single `jsonb` array; vertex order was not guaranteed before (ADR 0045).
- Anonymous users and drivers only see available vehicles; non-available vehicle details are visible to fleet staff,
  the device gateway and the driver using that vehicle (ADR 0046).
- The Mvc map gets vehicles from its own endpoint (role-aware, no 100-vehicle cap) and listens to every service-area
  group. Its hub token is signed with a separate key (`Jwt:HubKey`), carries no roles and is only valid on the hub;
  Mvc no longer holds `Jwt:Key`.
- User tokens carry the Identity security stamp; role changes, password changes and account deletion revoke them.
- `IdentityBootstrapper` only creates the first fleet manager and never promotes an existing account.
- Api, Worker and Mvc connect to Postgres as `scootly_app`; only the migrator and `db-init` use the superuser.
- Ride completion keeps the device-reported location when telemetry is fresh (2 minutes).
- Vehicles below 10% battery cannot be reserved.
- Outbox publish failures back off exponentially (up to 60 s) and log an error after 10 failed attempts.
- Worker and Mvc have Serilog level overrides (no more per-command SQL logs).
- Mvc vehicle list and dashboard fleet cards are for fleet staff only; pagination parameters are clamped.
- DataProtection keys for Mvc replicas are stored in Redis; nginx accepts request bodies up to 6 MB.

### Fixed
- RabbitMQ: a connection being recovered by the client library is no longer disposed (consumers stayed on dead channels).
- Error page rendered a duplicated text fragment.
- Unused session middleware logged errors on every health check during a Redis outage.
- Field-task creation race on the "one open task per vehicle and type" index.
- `X-Correlation-Id` is validated (max 64 safe characters) before being logged and echoed.

### Removed
- Dead code: `NoParkingZone`, `ParkingStation`, unused `HomeRegion` column (migration), Razor Pages leftovers,
  template pages and scripts, 48 unused client library files, unused package versions.

## [0.1.0-rc.1] - 2026-10-06

First release candidate. Work before day 91 is described by ADRs 0001-0030 in `docs/adr` and is not itemized here.
Known gaps are tracked in `docs/backlog/technical-debt.md`.

### Added
- Structured logging with Serilog (console and Seq) and a correlation id on every request.
- Distributed tracing with OpenTelemetry through an OTel collector to Jaeger, including trace propagation
  through the outbox so one trace spans the API request, the publish, the consume and the payment.
- Application metrics (rides started/completed, pending outbox messages) exported to Prometheus, and a Grafana dashboard.
- `/health/live` and `/health/ready` endpoints on Mvc, and a heartbeat-file health check for the Worker.
- Multi-stage Docker images for Api, Worker, Mvc, PaymentSimulator and the EF Core migrator; non-root user,
  version tags, health checks and graceful shutdown.
- Production Compose override: restart policy, resource limits, log rotation, `AllowedHosts`, closed infrastructure ports.
- Nginx reverse proxy with TLS in front of Mvc and two Api replicas, with a single entry point on `127.0.0.1:8443`.
- CI: coverage report, NuGet cache and Mvc image build.
- Release pipeline: a `v*` tag publishes the five images to GHCR and creates a GitHub Release from this file.

### Changed
- Console log output now includes event properties, so container logs can be searched by correlation id.
- Mvc content security policy `connect-src` is derived from `ApiBaseUrl` instead of a fixed local address.
- Mvc honours `X-Forwarded-*` headers from configured proxies only.

### Database
- 13 EF Core migrations, applied by the migrator image. Reverting `HardenDomainPaymentsAndMessaging` loses
  payment-tracking and reservation data; restore a pre-migration backup instead (see `docs/runbook/deployment.md`).