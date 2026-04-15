# ADR-0012: NuGet as Plugin Registry

**Status:** Accepted
**Date:** April 2026
**Decision:** NuGet is the package registry for Grob plugins. Packages tagged `grob-plugin`.

## Context

A plugin ecosystem needs a package registry. Options were: custom registry, GitHub releases, or an existing package manager.

## Decision

NuGet. Zero infrastructure to maintain. Versioning, hosting, push, search and download all provided. `grob search` queries NuGet for `grob-plugin` tagged packages.

## Alternatives Considered

**Custom registry** — significant infrastructure burden for a hobby project.

**GitHub releases** — no search, no versioning conventions, no dependency resolution.

## Consequences

Positive: zero infrastructure cost, familiar to C# developers, semantic versioning built in.
