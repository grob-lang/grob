# ADR-0004: Constrained Generics

**Status:** Accepted
**Date:** April 2026
**Decision:** The type checker and compiler understand generic type parameters internally. Users consume but cannot declare generic functions or types in v1.

## Context

OQ-001. `mapAs<T>()`, `filter`, `map` and typed collections require the compiler to understand generic type parameters. The question was how much generic infrastructure to expose to users.

## Decision

Constrained generics. The internal machinery exists. Users consume generic functions via stdlib and plugins. User-facing generic declarations are a post-MVP additive grammar extension.

## Alternatives Considered

**Full generics** — users can declare `fn identity<T>(x: T): T`. Non-trivial scope for v1.

**Special-cased** — `mapAs<T>()` as a one-off. Dead end — no path to typed `Stack<T>`.

## Consequences

Positive: type-safe collections and JSON deserialisation without committing to full generic syntax on day one. Closes no doors.

Negative: power users cannot write their own generic abstractions in v1. Acceptable — the stdlib covers the common cases.
