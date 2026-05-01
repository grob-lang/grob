# ADR-0006: No Pipe Operator

**Status:** Accepted
**Date:** April 2026
**Decision:** No `|` pipe operator inside Grob scripts.

## Context

Shell languages use `|` to pipe output between commands. Some modern languages
(F#, Elixir) have a pipe operator for function composition. The question was
whether Grob should have one.

## Decision

No pipe operator. Fluent method chaining is the in-script composition idiom:

```grob
results := files
    .filter(f => f.extension == ".log")
    .select(f => f.name)
    .sort()
```

Grob scripts can participate in OS-level shell pipelines as consumers —
`json.stdin()` and `csv.stdin()` read from process stdin. This is not a Grob
language feature; it is an OS mechanism surfaced through named functions.

## Alternatives Considered

**`|>` pipe operator (F# style)** — transforms `x |> f` to `f(x)`. Duplicates
what method chaining already provides. Adding a second composition syntax creates
a style split in the community with no functional benefit.

**`|` shell-style pipe** — would require fundamentally different execution
semantics (subprocesses, stream buffering). Not appropriate for a compiled
language with a type system.

## Consequences

Positive: one composition idiom, no style fragmentation. Method chaining is
already familiar to C# developers via LINQ.

Negative: users expecting a pipe operator from shell experience will not find
one. The leading-dot continuation syntax and fluent methods provide equivalent
expressiveness.
