# ADR-0013: Three-Tier Install Scope

**Status:** Accepted
**Date:** April 2026
**Decision:** Three install scopes — user-global (default), system-wide (`--system`), project-local (`--local`).

## Context

Where should plugins be installed? The dotnet tool model defaults to local. npm defaults to local. Go installs globally.

## Decision

User-global as default (no elevation, no manifest required). System-wide for shared machines. Project-local for version isolation. Resolution order: local → user → system.

## Alternatives Considered

**dotnet tool model** — local default requires a manifest for every script. Wrong model for reusable script dependencies.

## Consequences

Positive: no elevation by default, `grob install Grob.Http` works immediately, CI-safe via `grob restore`.
