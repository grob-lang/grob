# ADR-0006: No Pipe Operator

**Status:** Accepted
**Date:** April 2026
**Decision:** No `|` pipe operator inside Grob scripts. Fluent method chaining is the in-script pipe idiom.

## Context

Shell languages use `|` for pipeline composition. The question was whether Grob scripts should have a pipe operator for function composition.

## Decision

No pipe operator. Method chaining with leading-dot continuation is the idiomatic way to compose operations in Grob scripts. `json.stdin()` and `json.stdout()` exist for genuine shell pipeline composition at the process boundary.

## Alternatives Considered

**`|>` pipe operator** — F#/Elixir style. Adds grammar complexity for minimal benefit when method chaining already works well.

## Consequences

Positive: simpler grammar, no operator precedence complications, method chaining reads naturally for the target audience.

Negative: functional-style composition is more verbose. Acceptable — Grob targets C#/Go developers, not Haskell developers.
