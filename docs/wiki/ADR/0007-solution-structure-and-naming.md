# ADR-0007: Solution Structure and Naming

**Status:** Accepted
**Date:** April 2026
**Decision:** Six-project structure with full `Grob` prefix on all types. `GrobType`, `GrobValue` — not `GroType`.

## Context

The solution needed a clean architecture with strict dependency boundaries. The naming convention needed to be unambiguous.

## Decision

Six source projects: `Grob.Core`, `Grob.Runtime`, `Grob.Compiler`, `Grob.Vm`, `Grob.Stdlib`, `Grob.Cli`. Strict DAG — Compiler and Vm never reference each other. Full `Grob` prefix throughout. `Gro` abbreviation is not a Grob convention.

## Consequences

Positive: clean separation, testable assemblies, no circular dependencies.
