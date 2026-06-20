---
description: "Sprint 4 · Increment D — select/case. First-match, no fall-through, default optional, non-exhaustive (D-301), compiled as an equality + JumpIfFalse ladder. Crucially does NOT push a loop context, so break/continue inside a case fall through to an enclosing loop — the Increment B model gives this for free."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 4 · Increment D — `select` / `case`

Increment C delivered `for...in`. This increment adds the `select`/`case`
**statement** — first-match, no fall-through, `default` optional and
deliberately **non-exhaustive** (D-301, the intentional split from the
exhaustive switch *expression* that Increment E delivers). It compiles to an
equality + `JumpIfFalse` ladder over the conditional-jump machinery Increment A
built, and it is **not** a loop — so `break`/`continue` inside a case resolve to
an enclosing loop, which the Increment B loop-context model already handles if
`select` simply does not push a context.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 4** `select`/`case`
   bullets (first match, no fall-through, `default` optional;
   `break`/`continue` inside `select` apply to an enclosing loop), §3.3
   (`Equal`, `Jump`, `JumpIfFalse`), §3.1.1, §3.5.
2. `docs/design/grob-language-fundamentals.md` — **§3** (`select`/`case`) and
   the **"Why `select` is non-exhaustive"** subsection. Grep for the multi-value
   case form (`case Y, Z { }`) and the first-match / no-fall-through rule.
3. Decisions: **D-301** (`select` non-exhaustive; switch expression exhaustive —
   the split). Grep and confirm before relying on it.

> **Verify before relying on D-301 and §3.** Grep the decisions log for D-301
> and the fundamentals for §3. If D-301 has been superseded or §3 moved, surface
> it rather than proceeding.
>
> **`select`/`case` rules — inline reference (authoritative source is §3 and
> D-301).**
>
> - **`select (value) { case X { } case Y, Z { } default { } }`.** Evaluate the
>   subject once; compare against each case in order; the **first** match runs
>   its block and the `select` exits (no fall-through). `default` is optional and
>   runs only if no case matched.
> - **Multi-value case** — `case Y, Z { }` matches if the subject equals `Y` or
>   `Z`.
> - **Non-exhaustive (D-301).** Unlike the switch expression, a `select` with no
>   matching case and no `default` is **not** an error — it does nothing. Do not
>   add an exhaustiveness check; that is E's, on the expression form.
> - **Case-value type.** Each case value must be comparable to the subject type;
>   a case value of an incompatible type is a compile error (reuse the registry's
>   type-mismatch code — confirm **E0001**/**E0002**; do not invent).
> - **Compilation — an equality ladder.** Evaluate the subject (once, into a
>   slot or kept on the stack per the operator discipline); for each case, push
>   the case value, `Equal`, `JumpIfFalse` to the next case; the matched block
>   runs then `Jump`s to the end. A multi-value case is several `Equal` tests
>   ORed to the same block. All jumps backpatch through `patchJump`.
> - **`break`/`continue` inside a case** apply to an **enclosing loop**, not the
>   `select`. So **`select` must not push a loop context** — with B's stack,
>   `break`/`continue` then resolve to the nearest enclosing loop, or are the
>   outside-a-loop error (E2211/E2212) if there is none. Confirm `select` pushes
>   no context; if it does, that is the bug.
>
> **Sequencing note.** This is Increment D: A → B → C → **D (`select`)** → E
> (switch expression) → F (close). `select` reuses A's jump ladder and B's
> loop-context model. Do not pull the switch *expression* forward — `select` is
> a non-exhaustive statement; the exhaustive expression is E.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the case-value type check,
   the equality-ladder emission, the multi-value-case handling, the
   no-loop-context confirmation, and the tests — and wait for approval before
   editing.
2. On approval, `/start-branch` → propose `feat/select-statement`. Wait for the
   branch.
3. TDD — tests confirmed **failing** first.
4. `/commit-message` against staged changes.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop and surface.

## What is already done

- **Sprints 1–3 and Sprint 4 A–C:** the full pipeline through `for...in`, the
  conditional jump ladder (A), the `while` loop and loop-context stack (B). All
  green.

## Deliverable for this increment

The `select`/`case` statement across the type checker and the compiler.

1. **Type checker — case-value compatibility.** Each case value comparable to
   the subject type; an incompatible case value is a compile error (reuse the
   confirmed type-mismatch code). **No exhaustiveness check** — `select` is
   non-exhaustive by design. The §3.1.1 invariant holds for case-value and
   subject identifier nodes.
2. **Compiler — the equality ladder.** Subject evaluated once; per-case `Equal`
   + `JumpIfFalse` to the next case; matched block then `Jump` to the end;
   `default` as the fall-through tail; multi-value cases ORed to one block. All
   jumps backpatched through `patchJump`.
3. **Loop control — confirm pass-through.** `select` pushes **no** loop context;
   `break`/`continue` inside a case resolve to an enclosing loop via B's stack,
   or are E2211/E2212 if there is none. Add a test that proves it.

No new opcodes, no parser/AST edits. **No new error codes expected** — if a
`select` diagnostic seems to need a code that does not exist, stop and surface
rather than invent.

## Out of scope

The switch **expression** (Increment E) — `select` is the statement; the
exhaustive value-producing expression is E. The calculator close (Increment F).
Pattern matching beyond equality/value cases (not a v1 feature). Flow narrowing
(Sprint 5). Do not edit the parser, AST or `OpCode` enum.

## Tests

Per §3.5.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - First-match, no fall-through: only the first matching case's block compiles
    into the reachable path; a later matching case is not also run.
  - A `select` with no match and no `default` is **not** an error (D-301) and
    compiles to a no-op tail.
  - An incompatible case-value type is a compile error with a source location.
  - A multi-value `case Y, Z { }` disassembles to two `Equal` tests ORed to one
    block.
  - A `break`/`continue` inside a case resolves to an enclosing loop (compile a
    `while { select { case … { break } } }` and confirm the `break` exits the
    `while`); a `break` in a `select` with no enclosing loop is E2211.
  - §3.1.1 invariant holds.
- **VM tests (`Grob.Vm.Tests`):**
  - The first matching case runs and the `select` exits; `default` runs only
    when no case matches; no match and no `default` does nothing.
  - A multi-value case matches either value.
- **Integration tests (`Grob.Integration.Tests`):**
  - A script with a `select` (including a multi-value case and a `default`),
    inside a `while` with a `break`, runs end-to-end and produces the expected
    stdout.

## Acceptance

- `dotnet build` and `dotnet test` green.
- `select` is first-match, no fall-through, `default`-optional and
  non-exhaustive (no-match-no-default is a no-op, not an error).
- `break`/`continue` inside a case resolve to an enclosing loop, never to the
  `select`; `select` pushes no loop context.
- Case-value type mismatches are compile errors; no new codes invented.
- The §3.1.1 invariant holds; the DAG holds; no parser/AST/enum edits.

## Model

Sonnet 4.6 (High) throughout. The equality ladder reuses A's jump machinery and
the no-loop-context behaviour falls out of B's stack — this is composition of
two built pieces, settled by the inlined rules. No Opus carve-out.

## Hand-off

Summarise: the equality-ladder emission, the multi-value-case ORing, how
first-match / no-fall-through is enforced, the confirmation that `select` pushes
no loop context (and the test proving `break`/`continue` pass through), the
case-value type check and its code, and the test files added. Note for the next
chat: Increment E is the switch **expression** — `value switch { p => r, _ => d }`,
a value-producing **expression** with **exhaustiveness enforced by the type
checker** and all arms unifying to one type (reuse A's ternary arm-unification),
governed by D-277 and D-301.
