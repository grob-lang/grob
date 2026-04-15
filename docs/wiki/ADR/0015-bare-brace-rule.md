# ADR-0015: Bare Brace Rule

**Status:** Accepted
**Date:** April 2026
**Decision:** `{ }` is always a block. `#{ }` for anonymous structs. `TypeName { }` for named construction.

## Context

JavaScript-style languages have ambiguity between blocks and object literals when `{` appears at the start of a statement. The parser must guess intent.

## Decision

No ambiguity. Bare braces are always blocks. The `#` prefix signals an anonymous struct literal. Named type construction uses `TypeName { }`. The parser never guesses.

## Consequences

Positive: zero parsing ambiguity, clear visual distinction between blocks and data.
