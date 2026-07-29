# Correctness batch — Increment 1: type-checker tightening (void-in-arithmetic, unrecognised members)

**Branch:** `fix/type-checker-tightening`
**One concern:** reject two classes of program the type checker currently accepts permissively
— a `void`-returning call used as an arithmetic operand, and an unrecognised member name on a
receiver whose type is known. **Both are breaking changes**: they turn compiling programs into
compile errors.

Runs against the fresh corpus zip carrying D-356 through D-379. Corpus-first discipline
throughout; read the live decisions log and error-code registry tails, do not trust this prompt
or memory for D-### numbers or error codes. **The error-code count is 119** — confirm.

---

## Authority and context

Two findings from the pending correctness batch, both logged and deliberately deferred by the
increments that found them:

- **Void-in-arithmetic (D-362).** `arr.each(fn) + 1` compiles today. D-362 enumerated three
  legitimate permissive-`Unknown` operand sources reaching `EmitArithmetic` — the map element,
  the `Unknown`-receiver field, and the **void-returning call** (`ValidateArrayMethodCall`
  explicitly returns `Unknown` for "void"). The map-element source closed when D-374 built
  `MapTypeDescriptor`. The void source remains, and it is not legitimate: `void` in arithmetic
  is nonsensical, and rejecting it is strictly better than silently assuming `int`.
- **Unrecognised member on a known receiver (D-373).** `arr.garbage()` resolves permissively to
  `Unknown` rather than raising a diagnostic, because only recognised member names are matched
  and anything else falls through. **This means typos are not caught** — the highest user-facing
  cost in the batch.

**D-377 already did this for the property path.** It tightened bare-property access so that a
registered receiver type raises `E1002` for an unrecognised member, and walked every `GrobType`
to prove that only `Function` — permanently memberless by design — still reaches the permissive
fall-through. **This increment mirrors that tightening onto the method-call path.** Read D-377's
approach first; do not invent a second pattern.

---

## Plan-mode gate — read-only, STOP for approval before any source edit

1. **Establish current behaviour empirically**, per the D-366 precedent — build the CLI and run
   the cases rather than reasoning about them: `arr.each(fn) + 1`, `arr.garbage()`,
   `m.garbage()`, `"s".garbage()`, `(5).garbage()`. Report what each does today. Several
   increments have landed since these findings were logged, so **one or both may already be
   partly closed** — establish that before designing anything.
2. **D-377's property-path tightening** — how it decides "receiver type is known, so an
   unrecognised member is `E1002`" versus "receiver type is `Unknown`, so stay permissive".
   Report the mechanism; the method-call path should reuse it, not parallel it.
3. **The legitimate permissive cases that must survive.** An `Unknown`-typed receiver — most
   commonly an untyped lambda parameter (`xs.each(x => x.whatever())`) — must **stay
   permissive**, as must `?.` chains on a nullable or `Unknown` target. Enumerate every receiver
   kind that legitimately reaches the fall-through after this change, exactly as D-377 did for
   properties, and report the list.
4. **Where `void` is rejected.** `ResolveArithmetic` already raises **E0002** for `Struct` and
   `Lambda` operands (D-362's hard-error guard relies on that). Report where a void-returning
   call's operand type is available and whether it can join that existing rejection, so the same
   code and message shape covers it.
5. **The consequence for D-362's catalogue.** If the void source closes, D-362's enumerated
   permissive-`Unknown` operand sources drop from two to **one** (the `Unknown`-receiver field).
   Confirm the current count in `EmitArithmetic`'s comment — D-374 should have taken it from
   three to two — and report what it must become. If closing this leaves the hard-error guard
   able to cover more cases safely, report that; **do not widen the guard without saying so.**
6. **THE BREAKING-CHANGE ENUMERATION — required before any edit.** Both changes reject programs
   that currently compile. Enumerate **every** test, fixture, gold master and validation script
   that would newly fail, and report the list. A test may be **updated to assert the new correct
   behaviour**; it may **never be weakened or deleted** to accommodate the change. If the
   fallout is wider than a handful of sites, STOP and report rather than mass-editing.

Report the empirical findings, the reuse of D-377's mechanism, the surviving permissive cases,
the `void` rejection site, D-362's revised catalogue, and the breaking-change list. Then STOP.

---

## The fix — recommended, confirm or adjust in plan

1. **Void-in-arithmetic → `E0002`.** A `void`-returning call used as an arithmetic operand joins
   the existing `Struct`/`Lambda` rejection in `ResolveArithmetic`. Same code, same message
   shape. Update `EmitArithmetic`'s permissive-`Unknown` comment to the reduced catalogue.
2. **Unrecognised member on a known receiver → `E1002`.** Mirror D-377's property-path rule onto
   the method-call path: if the receiver's type is known and registered (array, map, string,
   `int`/`float`/`bool`, a nominal type), an unrecognised member name is `E1002`. If the
   receiver's type is `Unknown`, stay permissive.

---

## Scope boundaries — do NOT

- **Do not tighten the `Unknown`-receiver path.** Untyped lambda parameters and `?.` chains on
  `Unknown` targets are the legitimate permissive cases and must keep compiling.
- **Do not fix the other batch findings** — the `E5101` and `E0004` taxonomy items are the next
  increment; the `Synchronise()` double-diagnostic is a third; `for...in` snapshotting is D-379's
  own increment. Report anything new; fix none of them here.
- **Do not weaken or delete a test** to absorb a breaking change.
- **No new error code** — `E0002` and `E1002` both exist. Count stays **119**. **No new opcode.**

---

## Tests — TDD, red first, same commit

- **Void rejected:** `arr.each(fn) + 1` raises `E0002`; also as the right operand, and for `-`,
  `*`, `/`, `%`. A `void` call as a bare statement still compiles (that is its normal use).
- **Unrecognised members rejected:** `arr.garbage()`, `m.garbage()`, `"s".garbage()`,
  `(5).garbage()`, `(1.5).garbage()`, `true.garbage()` and a nominal receiver
  (`date.now().garbage()`) each raise `E1002`.
- **Permissive cases survive — the load-bearing tests:** `xs.each(x => x.whatever())` with an
  untyped lambda parameter still compiles; `?.` on a nullable or `Unknown` target still
  compiles. These are what a careless tightening breaks.
- **Every recognised member still resolves** — the full array, map, string and numeric member
  suites unchanged.
- Each updated test carries its new assertion, with the change visible in the diff.

---

## Gates

- pre-commit (TruffleHog, file hygiene, scoped `dotnet format --verify-no-changes`) and pre-push
  (`tooling/coverage-gate.ps1`, 80% line floor) green.
- CodeRabbit and SonarCloud clean on the PR. British English, no Oxford commas, never "simply".

---

## Landing — at close

- **Decisions log, three-location lockstep** (index row, full ADR entry, footer changelog),
  D-### from the **live registry tail** — next free is **D-380**; confirm, do not assume. Match
  the current index-row format (unpadded date cell, per D-377/D-378). The entry records: the two
  tightenings and the codes they reuse; the empirical before/after; the reuse of D-377's
  known-versus-`Unknown` receiver mechanism on the method-call path; the enumerated permissive
  cases that survive and why each is legitimate; D-362's permissive-`Unknown` operand catalogue
  reduced and its comment updated; and the full list of tests updated by the breaking change,
  each with what it now asserts. No new opcode, no new error code, count 119. Cite D-362, D-373,
  D-377, D-374.
- **Deliverable:** repo-pathed zip (source, tests, updated design docs). Archive this prompt
  under `prompts/archive/sprint-9/`.
