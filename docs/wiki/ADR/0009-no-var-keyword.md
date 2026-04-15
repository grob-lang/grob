# ADR-0009: No Var Keyword

**Status:** Accepted
**Date:** February 2026
**Decision:** `:=` declares and assigns. `=` reassigns. No `var`, `let` or `mut` keyword.

## Context

Variable declaration syntax is a fundamental language choice. Go uses `:=`. Kotlin uses `val`/`var`. Rust uses `let`/`let mut`.

## Decision

Go model. `:=` always declares in the current scope. `=` reassigns by walking the scope chain. `const` for immutable bindings. No keyword needed — the operator is the declaration.

## Consequences

Positive: less ceremony than `var x = 42` or `let x = 42`. Mandatory `:=` makes scope resolution unambiguous.
