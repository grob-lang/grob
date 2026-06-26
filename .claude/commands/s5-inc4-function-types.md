# Sprint 5 — Increment 4: function types as a type reference (D-326)

Branch: `feat/s5-inc4-function-types` — one concern, off `main`, `main` stays protected.

## Gate

Plan mode first. Produce a plan against the sequence below, post it, wait for explicit approval. No source change before approval. Full files in the diff, never patches. CodeRabbit in-loop; GPT-5.3 Codex cold-read against the merged branch before close.

## Objective

Make `fn(ParamTypes): ReturnType` a first-class type reference, accepted by `ParseTypeRef` anywhere a type is written. This closes a contradiction already in the spec: v1 requires an explicit return type on every function, and closures are first-class returnable values, so `makeCounter(): fn(): int` cannot be written — the only expressible return type is rejected with E2001. The canonical `makeCounter` example currently asserts syntax the parser refuses.

```grob
fn makeCounter(): fn(): int {     // currently E2001 on the return annotation
    count := 0
    return () => { count = count + 1; count }
}
```

## The decision — D-326 (self-contained)

`fn(ParamTypes): ReturnType` is a type reference. The contradiction is removed by making function types **expressible**, not by relaxing explicit return types (a larger v1 reversal that reopens deferred return-type inference) and not by dropping returnable closures (core identity). The consuming path — passing lambdas to built-in `.filter`/`.map`/`.select` — already works through inference against internal signatures and is unchanged.

- **Grammar:** `FnType := 'fn' '(' (TypeRef (',' TypeRef)*)? ')' ':' TypeRef`. `ParseTypeRef` gains an `fn` arm.
- **Suffix precedence:** `?` and `[]` bind to the **return type**. `fn(): int?` returns `int?`; `fn(): int[]` returns `int[]`. A nullable function, or an array of functions, needs parens: `(fn(): int)?`, `(fn(): int)[]`.
- **Identity:** structural — equal iff parameter arity, parameter types (positionally) and return type all match.
- **Assignability:** invariant. No variance in v1. `fn(int): int` is assignable only to `fn(int): int`. Nullable widening still applies (a non-nullable function value is assignable to the matching `T?` function slot); invariance governs the param/return structure only.
- **User surface is monomorphic:** concrete param/return types only, no type variables. The registry's internal `→` notation (`filter(fn: T → bool)`) is stdlib-internal generic machinery and is left as is.
- **Runtime: erased.** The callable is already a `GrobFunction`; the function type is compile-time only. No opcode, no `.grobc`, no `GrobValue` change.

## Verify first (do not rely on memory)

Confirm each against the live `grob-decisions-log.md` tail before coding:

- **D-326** — this increment. Confirm it is the entry you are implementing and nothing supersedes it.
- **D-296** — four-category capture / closures. This increment completes the closure story at the type-annotation level.
- **D-080** — constrained generics: users consume generic functions, declare none. Function types are **not** generics; this rule is untouched and must not be loosened.
- **D-166 / D-323** — multi-pass type checker. Function-type annotations resolve in **pass-1 signature registration**; phase 1.5 is not involved.
- **D-308** — diagnostics raised against `ErrorCatalog` descriptors, never code literals. Confirm the exact mismatch descriptor before using it.
- **D-320** — reserved-identifier mechanism. Unrelated, but confirms `fn` remains a hard keyword; no interaction expected.

## Load-bearing rules (inline reference)

**§9 rule being satisfied.** Explicit return types are required on all functions in v1; return-type inference is post-MVP. This increment does **not** relax that — it makes the required return type *writable* when it is a function.

**Parser disambiguation.** `fn` at statement head is a declaration; `fn` in type position (after `:` in an annotation, a parameter type, a return type, a field type) is a type. The positions never overlap, so the `fn` arm in `ParseTypeRef` introduces no ambiguity.

**Checker.** Introduce a `FunctionType` `GrobType` with structural equality over (param types, return type) and the invariant assignability rule. A function-type annotation resolves during pass-1 signature registration — it references type names pass 1 already knows; no initialiser-dependency ordering.

**Error policy.** A function-type mismatch is an ordinary assignment/argument type error and reuses the existing mismatch family — confirm the exact descriptor against `ErrorCatalog`/`grob-error-codes.md`. Malformed function-type syntax reuses the existing syntax codes. **No new error code.** Count stays at 109. If a genuinely new diagnostic seems required, **stop and surface**.

## Closed surface — do not touch

- The VM, opcodes, `.grobc`, `GrobValue` — function types are erased (D-303).
- Generics / type variables at the user surface (D-080).
- Explicit-return-type rule; do not add return-type inference (post-v1).
- Phase 1.5 of the checker (D-323).
- The error catalog / error-code count (D-308, D-316). No additions.

## TDD sequence

1. **Parser tests first:**
   - `fn(): int`, `fn(int, string): bool`, nested `fn(fn(int): bool): void`.
   - Suffix precedence: `fn(): int?` parses as function-returning-`int?`; `fn(): int[]` as function-returning-`int[]`; `(fn(): int)?` and `(fn(): int)[]` as nullable/array of function.
   - `makeCounter`'s `fn(): int` return annotation parses (no E2001).
   - Malformed: `fn(): ` (missing return type), `fn(: int)` — surface the existing syntax code, not a fault.
2. **Checker tests:**
   - Structural equality: two identical function types are assignable; differing arity, parameter type or return type are not — and produce the confirmed mismatch descriptor.
   - Invariance: no covariant/contravariant assignment is accepted.
   - Nullable widening to a function `T?` slot is accepted.
   - A user function with a callback parameter (`fn each(items: int[], action: fn(int): void)`) type-checks and accepts a lambda argument.
   - `makeCounter` type-checks; the returned closure's type matches `fn(): int`.
3. **End-to-end:** `makeCounter` compiles and runs — the example becomes a passing test, no longer aspirational.
4. **Green.** Parser, checker and existing suites pass; no regression elsewhere.

## Acceptance criteria

- The `makeCounter` example parses, type-checks and runs.
- Function types are accepted in parameter, return, binding and element-type positions, with the specified suffix precedence.
- Structural identity and invariant assignability hold; mismatches use the confirmed existing descriptor.
- Error-code count unchanged at 109; the D-316 drift gate is green.
- Spec edits included: `grob-language-fundamentals.md` §9 gains the function-type form in the type-reference grammar (citing D-326); `grob-type-registry.md` documents `FunctionType` (structural identity, invariant assignability) and cross-references D-296. Reconcile the registry `→` notation against the surface `fn(...): T` form as a documentation tidy-up.
- CodeRabbit clean; Codex cold-read raises nothing load-bearing.

## Stop-and-surface

- Any apparent need for a new error code.
- Any pressure to add return-type inference or relax the explicit-return-type rule to make a case work.
- Any pressure to introduce a type variable at the user surface (would breach D-080).
- A variance case that invariant assignability cannot express cleanly — surface it for a deliberate post-v1 call rather than adding variance here.
