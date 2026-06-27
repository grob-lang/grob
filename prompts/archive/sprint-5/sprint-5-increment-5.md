---
description: "Sprint 5 Increment 5 — array type-refs: the [] suffix as a type-reference production (D-327)"
---

# Sprint 5 — Increment 5: Array type-refs (`[]` as a type-reference production)

Post-close interlude increment, following Increment 3 (D-325, location-based upvalue
closing) and Increment 4 (D-326, function types as a first-class type reference). This
closes a defect Increment 4 surfaced and must merge green **before the cold-reader QA
pass** — the spec's own pervasive `int[]` type annotations do not currently parse.

**Model.** Sonnet 4.6 (default). This is a bounded parser plus type-checker change, not
a structural design decision. Escalate to Opus only if the suffix-precedence interaction
with the D-326 function-type grammar proves load-bearing during planning — surface it, do
not switch silently.

---

## 0. Verify first — do not write a line of source until this block passes

Read the **live uploaded** corpus, not memory or a stale snapshot.

1. Open `grob-decisions-log.md` and confirm **D-327** exists in all three locations:
   summary index row, full entry immediately before `## Post-MVP Decisions`, and the
   newest-first changelog block. Confirm the live tail is **D-327** and the summary index
   reads `… D-325, D-326, D-327`. If the tail is not D-327, **stop and surface** — the
   number was taken against the wrong baseline.
2. Open `grob-language-fundamentals.md` §9 and confirm the type-reference grammar is the
   **primary-plus-suffix** form (`TypeRef := TypePrimary TypeSuffix*`, `TypeSuffix := '[' ']' | '?'`).
   The implementation is written to **this** grammar. If §9 still shows the three-production
   D-326 grammar with no `[]`, **stop and surface** — the spec edit did not land.
3. Confirm the error-code canonical total is **109** in `grob-error-codes.md` and that the
   `Grob.Consistency.Tests` count assertion (D-316) currently agrees. This increment must
   leave the count at **109**.
4. Confirm against the parser source that `ParseTypeRef` (or its equivalent) does **not**
   currently consume `[` in type position, and that **value-position** `[` (array literal,
   indexing) is parsed by a separate path. This is the defect; name the exact call sites in
   the plan.
5. Grep the thirteen release-gate scripts (`grob-sample-scripts.md` and the on-disk fixture
   copies) for `[]`-style type annotations in parameter, return, binding and field positions.
   Record which scripts use them — they have never parsed, which confirms the parser has not
   been exercised against the validation suite. Note the count in the plan; do not change the
   scripts in this increment unless one is malformed, in which case **surface** it.

Do not proceed past this block on any failed check. Surface it.

---

## 1. Plan-mode gate

Produce a **numbered plan** and stop for approval before any source change. The plan must
name, concretely:

- the exact parser entry point and the shape of the suffix loop;
- the AST node(s) for array and nullable type-refs (or the single suffix-wrapping node),
  each carrying `SourceLocation` (D-137) spanning the `[` `]` / `?`;
- the type-checker resolution path from an array annotation to the **existing** `T[]` type;
- the precise test matrix (section 4 below), written first;
- the negative/recovery behaviour for `int[` and `int[5]`, with the exact `ErrorCatalog`
  descriptors to be reused (confirmed in-increment per D-308);
- confirmation that no item on the closed surface (section 3) is touched.

No source edits until the plan is approved.

---

## 2. What to build

Implement the §9 / D-327 grammar:

```ebnf
TypeRef     := TypePrimary TypeSuffix*
TypeSuffix  := '[' ']'                  // array of the preceding type
             | '?'                       // nullable
TypePrimary := Identifier TypeArgs?
             | 'fn' '(' (TypeRef (',' TypeRef)*)? ')' ':' TypeRef
             | '(' TypeRef ')'           // grouping
```

- **Parser.** `ParseTypeRef` parses a primary, then loops left-to-right consuming `[` `]`
  and `?`. The grouping primary `'(' TypeRef ')'` **replaces** D-326's dedicated
  `'(' TypeRef ')' '?'` production — it is strictly more expressive and is the only way to
  suffix a function type. A leading `(` with no preceding `fn` is grouping; there is no
  ambiguity with the `fn` parameter list. `[` is consumed in **type** position only — the
  value-position `[` path (array literal, indexing) is untouched.
- **AST.** An array type-ref wraps an element type-ref; a nullable type-ref marks its inner
  type-ref nullable. Both carry `SourceLocation`. `int[]?` and `int?[]` produce structurally
  different trees.
- **Type checker.** An array annotation resolves to the `T[]` array type the checker
  **already owns** — value-position literals and `:=` inference already construct it, with
  its full member registry (`.select`, `.append`, `.format.*`, mutation rules). Wire the
  annotation path to that representation; introduce **no** new runtime type. Resolution runs
  during **pass-1 signature registration** alongside other type-name references — there is no
  initialiser-dependency ordering, so D-323's phase 1.5 is **not** involved.
- **Nesting.** `int[][]` and `int[][][]` resolve as arrays of arrays (D-182). No rectangular
  guarantee, no dimension type.

---

## 3. Closed surface — do not touch

- **Opcodes, `.grobc` format, `GrobValue`.** An array type-ref is a compile-time annotation
  over the existing array value representation. Runtime is **erased**. If you find yourself
  reaching for an opcode or a `GrobValue` variant, **stop and surface** — the design is wrong.
- **The D-326 function-type checker logic.** Structural identity and invariant assignability
  are settled. This increment adds the `[]` suffix production and its resolution; it does not
  re-open function-type identity or variance.
- **The three-pass checker phase structure (D-323).** Array resolution slots into pass 1.
  Do not touch phase 1.5 (value-binding type resolution) or pass 2 ordering.
- **The error-catalog count.** It stays at **109**. Reuse existing parser-error descriptors
  for malformed suffixes. A new code is a **stop-and-surface** decision, not an in-increment
  liberty (see section 5).
- **Value-position `[` parsing.** Array literals and indexing are correct today. Do not
  refactor them into the type path.
- **The thirteen release-gate scripts.** Do not edit them to make them parse; if one is
  malformed, surface it.

---

## 4. Tests first (TDD)

Write these before the parser change. Each must fail for the right reason, then pass.

**Positions** — `int[]` parses and type-checks as an annotation in every position:
- parameter type: `fn f(xs: int[]): int { … }`
- return type: `fn g(): string[] { return [] }`
- binding annotation: `items: int[] := []`
- struct field type: `type T { tags: string[] }`

**Suffix precedence** — distinct trees, both parse and check:
- `int[]?` (nullable array of int) vs `int?[]` (array of nullable int) — assert the two
  are **not** the same type
- `fn(): int[]` (function returning `int[]`) vs `(fn(): int)[]` (array of functions) —
  assert the parens change the parse
- `(fn(): int)?` (nullable function) still parses under the generalised grouping primary

**Nesting** (D-182):
- `int[][]`, `int[][][]` resolve as arrays of arrays; element access type-checks

**Negative / recovery** (D-300 — error-recovering parser, stateless, no diagnostic cap):
- unterminated `xs: int[ := []` synchronises to the statement boundary, emits an
  `ErrorDecl`/`ErrorStmt` typed `Error`, with the standard expected-`]` diagnostic, and
  does **not** cascade
- fixed-size `xs: int[5]` is rejected — Grob has no fixed-size array types — with a
  diagnostic that points toward `int[]`
- gold-master pairs (`_grob.txt` / `_expected.txt`) for both, harnessed and rich, recording
  the real diagnostic output

**makeCounter-adjacent** — a named function whose return type is an array of a function type
or a function returning an array, exercising the D-326 × D-327 interaction at the parser and
checker level.

---

## 5. Stop-and-surface triggers

- The error-message quality bar suggests `int[5]` deserves a **bespoke** descriptor
  ("fixed-size array types are not supported — use `int[]`") rather than the generic
  expected-token error. That is a deliberate, separately-logged addition that moves the count
  off 109 — **surface it for a decision**, do not add it unprompted.
- Any release-gate script turns out to depend on syntax this grammar still does not accept.
- The suffix loop interacts with `TypeArgs` parsing (`map<string, int>[]`) in a way the plan
  did not anticipate — confirm `map<…>[]` (array of maps) parses, and surface if the
  angle-bracket / square-bracket interaction is ambiguous.
- Wiring array resolution appears to require a phase-1.5 touch (D-323). It should not — if it
  does, the design is off; stop.

---

## 6. Definition of done

- Plan approved before any source edit. One concern, one branch, one PR.
- All section 4 tests green; full suite green; `Grob.Consistency.Tests` green with count at 109.
- `grob fmt` idempotent on all new fixtures; pre-push gate clean.
- §9 (already edited this session) is the implemented grammar — no drift between spec and parser.
- D-327 cited in the PR description. No decisions-log edit in this increment — D-327 is already
  logged; this increment implements it.
- CodeRabbit review addressed. Ready for the GPT-5 Codex cold-read against the merged branch.
