# ADR-0016: Const Means Fully Immutable

**Status:** Superseded by D-288 and D-291 — see "What Changed Since This ADR" below
**Date:** April 2026
**Decision:** `const` prevents both rebinding and mutation. One rule: `const` means immutable.

## Context

JavaScript `const` prevents rebinding but allows mutation of the referenced object. Rust separates binding mutability from content mutability. The question was which model Grob should use.

## Decision

`const` is fully immutable — no rebinding, no mutation. `const items := [1, 2, 3]` means `items.append(4)` is a compile error. Non-`const` bindings allow both rebinding and mutation. The deeper question of mutable-binding-with-immutable-content is deferred post-MVP.

## Consequences

Positive: one simple rule. No JavaScript-style surprises where `const obj = {}; obj.x = 1` works.

Negative: no way to express "I want to mutate the contents but not rebind the name" in v1. Acceptable — scripts rarely need that distinction.

## What Changed Since This ADR

This ADR documented the original "one rule" `const` model. That model was
revisited during the pre-implementation review and split into two keywords by
**D-288** and **D-291** (April 2026). The split, not this ADR, describes
current language semantics.

**Current model — D-288:**

- **`const`** is now restricted to compile-time constant expressions only
  (literals, arithmetic on literals, references to other `const`-bound
  identifiers, and a small whitelist of stdlib constants). Compile-time
  constants are evaluated by the type checker and inlined at every reference
  — there is no runtime binding to mutate, so the deep-immutability question
  no longer applies to `const`.
- **`readonly`** is the new keyword for runtime-once bindings whose right-hand
  side is any valid Grob expression (function calls, struct construction,
  array literals, anything). `readonly` is what users reach for when they
  would have reached for `const` under this ADR's model.

**Current model — D-291:**

- The deep-immutability rule documented in this ADR — that the binding
  rejects both rebinding and mutation — moves to `readonly`. A `readonly`
  array rejects `.append()`, a `readonly` map rejects index-write, a
  `readonly` struct rejects field assignment. This is exactly the contract
  this ADR specified for `const`; only the keyword name has changed.

**Migration rule (D-290):** an existing `const` declaration becomes
`readonly` if and only if its right-hand side is not a compile-time constant
expression. RHS that is a literal or arithmetic on literals stays `const`.
RHS that is an array literal, function call, or struct construction becomes
`readonly`.

**Why split:** value patterns in switch expressions require compile-time
constants; the type checker and bytecode pipeline need to know at parse time
which once-bound names are inlinable and which require runtime evaluation.
The single-keyword model in this ADR conflated the two and made that
distinction implicit, which became architecturally awkward as switch
expressions and the constant pool design firmed up.

For current language semantics see the decisions log entries D-288, D-289
(definition of "compile-time constant expression"), D-290 (migration rule),
D-291 (`readonly` semantics), D-292 (function-local scope), and the spec at
`grob-language-fundamentals.md` §24.
