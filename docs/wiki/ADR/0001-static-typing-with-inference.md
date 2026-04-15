# ADR-0001: Static Typing with Inference

**Status:** Accepted
**Date:** February 2026
**Decision:** Grob is statically typed with type inference on local variable declarations.

## Context

The design target is a scripting language that fills the gap where Python's dynamic typing is a weakness and Go's mandatory annotations are too ceremonious. Type safety matters for scripting — wrong types cause runtime failures in unattended scripts.

## Decision

Static types resolved at compile time. The compiler infers types from the right-hand side of `:=` declarations. Explicit annotations are required only where inference cannot resolve: `nil` initialisations and empty array literals.

## Alternatives Considered

**Dynamic typing** — lower ceremony but errors appear at runtime. The entire design target of Grob is to fill the gap where dynamic typing is the weakness.

**Mandatory annotations** — maximum clarity but too verbose for scripting. Inference is the right model.

## Consequences

Positive: compile-time error detection, method-call sugar with no boxing overhead, better tooling support.

Negative: the type checker is a significant implementation investment. These costs are justified by the language's identity.
