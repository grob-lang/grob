# ADR-0014: Source Locations from Day One

**Status:** Accepted
**Date:** April 2026
**Decision:** Every AST node carries a `SourceLocation` from the first line of compiler implementation.

## Context

The LSP needs source locations for go-to-definition, hover and diagnostics. Retrofitting source locations after the compiler is built requires a full type checker audit.

## Decision

`SourceLocation` on every AST node. Identifier nodes carry declaration back-references set by the type checker. This is a day-one compiler construction requirement, not a tooling phase concern.

## Consequences

Positive: LSP capabilities are trivial to implement when the time comes.

Negative: one extra field per AST node. Negligible cost.
