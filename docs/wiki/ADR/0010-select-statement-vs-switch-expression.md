# ADR-0010: Select Statement vs Switch Expression

**Status:** Accepted
**Date:** April 2026
**Decision:** `select/case` is the multi-branch statement. `value switch { }` is the multi-branch expression. Two distinct forms.

## Context

Many languages overload `switch` for both statement and expression contexts, creating ambiguity.

## Decision

`select` is always a statement — executes blocks, no value produced. `switch` is always an expression — each arm produces a value. The parser is never ambiguous.

## Consequences

Positive: no parsing ambiguity, clear intent at the call site.
