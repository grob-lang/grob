# ADR-0005: C# GC over Custom

**Status:** Accepted (tentative — pending clox completion)
**Date:** February 2026
**Decision:** Lean on C#'s garbage collector rather than implementing a custom mark-and-sweep.

## Context

The VM is written in C#. C#'s GC handles heap memory automatically. A custom GC adds significant complexity.

## Decision

Use C#'s GC. Minimise heap allocations through value representation — `struct` for value types, `class` only for heap objects.

## Alternatives Considered

**Custom mark-and-sweep** — full control, but significant implementation effort with marginal benefit for scripting workloads.

## Consequences

Positive: simpler implementation, proven GC, no GC bugs to debug.

Negative: less control over collection timing. Acceptable for scripting use cases where GC pauses are invisible.
