# ADR-0003: Exceptions over Error Values

**Status:** Accepted
**Date:** April 2026
**Decision:** Exceptions are the runtime error model. `try/catch` with typed catch blocks.

## Context

OQ-004 required choosing between exceptions, Go-style error values, and a `Result<T>` type for runtime error handling.

## Decision

Exceptions. Functions throw on failure. Unhandled exceptions propagate to the VM top level. `try/catch` with typed catches for recovery. Exception hierarchy: `GrobError` as root with typed leaves (`IoError`, `NetworkError`, `JsonError`, `ProcessError`, `NilError`, `RuntimeError`).

## Alternatives Considered

**Go-style error values** — every stdlib function returns `(result, error)`. Verbose for scripting, forces nil-checking on every call.

**`Result<T>` type** — Rust-style. Requires generics and pattern matching infrastructure. Over-engineered for a scripting language.

## Consequences

Positive: familiar to C#/Java/Python developers, clean error propagation, typed catches for granular recovery.

Negative: exceptions can be performance-costly on the throw path. Acceptable for scripting workloads where errors are exceptional, not routine.
