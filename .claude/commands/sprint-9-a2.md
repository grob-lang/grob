---
description: "Sprint 9 · Increment A2 — array index-write emission (the write companion to A). Resolve the undocumented 'Index targets are deferred' early-return in Compiler.Statements.cs so `arr[i] = v` compiles and runs. Establish the blast radius first (does the same early-return also disable `map[k] = v` and `point.x = v` as assignment statements). Store-element emission on the assignment-target path, E5101 bounds on the write side, E0204 rejection of writes to readonly/const arrays at compile time. Turn the code-comment deferral into a logged, resolved decision. One concern; no stdlib."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 9 · Increment A2 — array index-write emission

Increment A added array index **read** emission and left index **write** deferred — its
verify step confirmed the deferral is a deliberate early-return in
`Compiler.Statements.cs` ("Index targets are deferred"), not an accidental gap. That
deferral is **undocumented** — no decision, no open question, no requirements or
release-gate note owns it — and it disables a **documented v1 feature**: the spec lists
`ITEMS[0] = 99`, `CONFIG["port"] = "8080"` and `point.x = 5` as mutations that are
compile errors *on a `readonly` binding*, which means on a mutable binding they are
expected to work. A scripting language cannot ship v1 with array element assignment
broken. This increment closes the array-indexer concern whole — read landed in A, write
lands here — and turns the code-comment deferral into a logged, resolved decision.

Read, in order:

1. `docs/design/grob-language-fundamentals.md` — the assignment statement grammar, the
   `readonly` mutation-error list (§ where `ITEMS[0] = 99`/`CONFIG["port"] = "8080"`/
   `point.x = 5` appear as errors *on readonly bindings*, establishing they work on
   mutable ones), and §9 (no uninitialised variables).
2. `docs/design/grob-vm-architecture.md` — the statement compiler's assignment-target
   handling (`Compiler.Statements.cs` — the "Index targets are deferred" early-return),
   the value-stack model, the array index **read** emission Increment A added
   (`VisitIndex`), and how the **map** write path (`m[key] = value`, D-112) emits or
   desugars today — the precedent to mirror or diverge from.
3. `docs/design/grob-error-codes.md` — `E5101` (array index out of range → `IndexError`,
   reused on the write side), `E0204` (mutation of a `readonly` value), and `E0201`/
   `E0202` (const/readonly reassignment) so the target-rejection diagnostic is the right
   one.
4. Decisions: **D-112** (array/map indexing — map read **and** write), **D-140** (array
   mutation methods; mutation on `const` = compile error), **D-289** (readonly deep
   immutability — `X["k"] = v` on a `readonly map` is a compile error, the exact precedent
   for the array target), **D-345** (the wholesale `VisitIndex` gap), **D-347** (A's read
   emission this builds on). Grep for the **next-free D-number** at the time this runs —
   A closed at D-348, so D-349 if this runs immediately, higher if it runs late in the
   sprint after D/E/G. Confirm against the live tail; do not pre-allocate from memory.

> **Verify before relying on cited decisions and sections.** Grep `Compiler.Statements.cs`
> and the map write path before editing. If the map write emits differently than this
> prompt assumes, surface it.
>
> **Blast-radius probe first — grammar-first gate (D-331).** "Index targets are deferred"
> reads broad. Before any emission work, establish **exactly which assignment-target forms
> the early-return disables**: `arr[i] = v` (confirmed), and confirm whether `map[k] = v`
> and `point.x = v` as assignment **statements** hit the same early-return or route around
> it (e.g. the type checker desugaring `m[k] = v` to `m.set(k, v)` before it reaches the
> statement compiler). If the whole index/field-assignment-target surface is dead, that is
> a larger v1 hole than one array feature — bring the also-deferred targets up in this
> same increment (they are one concern — assignment-target emission). If map/struct
> desugar around it and array is the lone casualty, scope to array. Surface the finding
> and the resulting scope before editing.
>
> **The write emission — inline reference.**
>
> - **Array index-store on the assignment-target path.** Emit the receiver, the index and
>   the value, then the array index-store — the write arm of the same construct A's read
>   emission handles. Arrays have no public `set(i, v)` method (their mutators are
>   `append`/`insert`/`remove`/`clear`, D-140), so unlike maps this is genuine index-store
>   emission, not a desugar to a method call. Reuse whatever store the map write path
>   already emits if it applies; if a store opcode is genuinely required and absent, that
>   is the `adding-an-opcode` procedure (D-331) and a **surfaced** decision — the `OpCode`
>   enum is closed (§3.3, ADR-0013), so do not grow it silently.
> - **Bounds on the write side → `IndexError` via `E5101`.** An out-of-range write index
>   raises `IndexError` through the **existing** `E5101` and the Sprint-7 handler table
>   (D-334) — the same code and the same mechanism as the read side, no bespoke path.
>   `arr[99] = 1` on a length-3 array, unhandled, produces the quality diagnostic
>   (`file:line`, D-322) and exit 1; caught by `catch (e: IndexError)` it resumes.
> - **`readonly`/`const` rejection at compile time → `E0204`.** `arr[i] = v` on a
>   `readonly` or `const`-bound array is a **compile error** at the existing `E0204`
>   (mutation of a readonly value), mirroring the `X["k"] = v` on `readonly map` precedent
>   the log already sets (D-289, and D-140 for `const`). This is type-checker work on the
>   assignment target, not emission — the write only reaches the VM for a mutable binding.
> - **RHS type-checking.** The assigned value's type must match the array element type;
>   a mismatch is the existing type-mismatch code. `ResolvedType`/`Declaration` on the
>   target node (§3.1.1).
> - **In-place mutation.** For a mutable array the write mutates in place (arrays are
>   reference values; D-303), consistent with the mutation methods (D-140).

> **Sequencing note.** This is the write companion to Increment A, one concern with it —
> the array indexer's emission. It does not block the stdlib increments (D–G) and can run
> any time after A; run it before the Sprint 9 close (Increment H) so the indexer concern
> lands whole within the sprint rather than trailing into Sprint 10.

## What you're building

1. **The blast-radius finding.** Which assignment-target forms the early-return disables,
   and the resulting scope (array-only, or array plus any also-deferred map/struct
   targets brought up together).
2. **Array index-store emission** on the assignment-target path, resolving the "Index
   targets are deferred" early-return for the confirmed-broken targets. `ResolvedType`/
   `Declaration` on the target node.
3. **Bounds → `IndexError`** on the write side via the existing `E5101` and the handler
   table.
4. **`readonly`/`const` rejection** of index-target writes at the existing `E0204`
   (compile-time), mirroring the map/struct precedent.
5. **RHS type-checking** against the element type.
6. **The deferral-resolution decision.** An append-only entry at the next-free D-number
   in three-location lockstep, recording that the previously-undocumented index-target
   assignment deferral is now resolved (or, if any target is still legitimately out of
   scope, naming it and its v1 target so it is scheduled rather than left as a comment).

No new opcode unless the blast-radius work genuinely requires a store opcode the map path
lacks — then surfaced via `adding-an-opcode` (D-331). No stdlib code. No new error code —
`E5101`, `E0204` and the type-mismatch code all pre-exist.

## Out of scope

Every Sprint 9 stdlib module. Compound index assignment (`arr[i] += v`) if it is a
distinct construct not covered by the base write path — confirm and, if separate, surface
it rather than silently including or excluding it. Do not grow the `OpCode` enum without
surfacing.

## Tests

- **Type-checker tests (`Grob.Compiler.Tests`):**
  - `arr[0] = 99` on a mutable array type-checks; the target node carries a non-null
    `ResolvedType` and `Declaration` (`Assert.Same`, §3.1.1).
  - `arr[0] = 99` on a `readonly` array is `E0204`; on a `const` array is the const-
    mutation error — the map/struct precedent holds for arrays.
  - `arr[0] = "x"` on an `int[]` is the element-type mismatch.
  - Whatever map/struct assignment-target behaviour the blast-radius probe found, asserted
    (working, or brought up with the array target).
- **VM tests (`Grob.Vm.Tests`):**
  - `arr[1] = 20` on `[10, 20, 30]` mutates in place; a subsequent read returns the new
    value.
  - `matrix[r][c] = v` (chained target) writes correctly.
  - `arr[99] = 1` on a length-3 array, unhandled, unwinds to the top level with the
    quality diagnostic (`IndexError`, file, line) and exit 1; inside `catch (e:
    IndexError)` it is caught and the script resumes; a `finally` around it runs exactly
    once.
- **Integration / spec-consistency:** D-316 green; the deferral-resolution decision in
  three-location lockstep; the count reconciled (no new code expected); no `OpCode` growth
  unless surfaced and approved.

## Acceptance

- `arr[i] = v` compiles and runs on a mutable array, mutating in place; the "Index targets
  are deferred" early-return no longer blocks the confirmed-broken targets.
- Out-of-bounds write throws a catchable `IndexError` through the handler table via the
  existing `E5101`; unhandled it produces the quality diagnostic and exit 1.
- `arr[i] = v` on a `readonly`/`const` array is a compile error at `E0204`/the const-
  mutation code; an element-type mismatch is caught.
- The blast-radius finding is recorded; any target still out of scope is logged with a v1
  target, not left as a code comment.
- No new opcode (unless surfaced and approved), no new error code, no stdlib code; the
  deferral-resolution decision logged in lockstep; D-316 green; coverage at or above 90%
  on the affected projects — the write and rejection paths especially.

## Model

Sonnet 4.6 (High). Store emission over the value-stack machinery A's read emission
established, plus type-checker rejection over the existing readonly/const precedent — no
Opus carve-out.

## Hand-off

Summarise: the blast-radius finding and the scope it set; the array index-store emission
and whether it reused the map store or needed its own; the `E5101` bounds path on the
write side; the `E0204` readonly/const rejection; the deferral-resolution decision and
its lockstep entry. The array-indexer concern (read in A, write here) is now whole; note
any remaining assignment-target work scheduled for a named target rather than deferred by
comment.
