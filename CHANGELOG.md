# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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