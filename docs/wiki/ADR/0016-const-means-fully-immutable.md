# ADR-0016: Const Means Fully Immutable

**Status:** Accepted
**Date:** April 2026
**Decision:** `const` prevents both rebinding and mutation. One rule: `const` means immutable.

## Context

JavaScript `const` prevents rebinding but allows mutation of the referenced object. Rust separates binding mutability from content mutability. The question was which model Grob should use.

## Decision

`const` is fully immutable — no rebinding, no mutation. `const items := [1, 2, 3]` means `items.append(4)` is a compile error. Non-`const` bindings allow both rebinding and mutation. The deeper question of mutable-binding-with-immutable-content is deferred post-MVP.

## Consequences

Positive: one simple rule. No JavaScript-style surprises where `const obj = {}; obj.x = 1` works.

Negative: no way to express "I want to mutate the contents but not rebind the name" in v1. Acceptable — scripts rarely need that distinction.
