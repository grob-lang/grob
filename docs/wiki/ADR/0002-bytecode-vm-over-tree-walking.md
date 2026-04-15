# ADR-0002: Bytecode VM over Tree-Walking

**Status:** Accepted
**Date:** February 2026
**Decision:** Grob uses a stack-based bytecode VM, not a tree-walking evaluator.

## Context

SharpBASIC used a tree-walking evaluator. This is the simplest execution model but incurs overhead from tree traversal and C# call stack usage.

## Decision

A stack-based bytecode VM. The compiler walks the AST and emits a flat instruction stream. The VM is a tight fetch-decode-execute loop.

## Alternatives Considered

**Tree-walking** — simpler but slower. SharpBASIC demonstrated the approach's ceiling.

**Transpilation to C#** — deferred as a post-MVP option (`grob build`).

**JIT compilation** — explicitly out of scope.

## Consequences

Positive: significant performance improvement, clean separation of compilation and execution.

Negative: more complex implementation — backpatching, call frames, instruction set. Justified by clox implementation experience.
