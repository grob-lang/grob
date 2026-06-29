---
description: "Sprint 6 · Increment E — sprint close. The types.grob smoke script (declarations, construction with defaults, nested construction, field access and assignment, a recursive type, a #{ } projection, a closure-in-field escape), the §7 acceptance, and the fourth VM-execution benchmark baseline against the two-axis gate (D-313)."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 6 · Increment E — sprint close

Increments A–D delivered user-defined types: declarations and the registry (A),
construction and defaults (B), field access and assignment (C), anonymous structs (D).
This increment **closes the sprint**: a `types.grob` smoke script exercising the whole
§7 surface end-to-end, the §7 acceptance met, and the fourth VM-execution benchmark
baseline taken and passed against the two-axis gate. It is the Sprint 6 analogue of
Sprint 5's `functions.grob` close.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **§7 acceptance block** (types declared,
   constructed and accessed; anonymous structs in lambdas and `select()` projections;
   the type checker catches undefined field access), §3.5 (test routing).
2. `docs/design/grob-benchmarking-strategy.md` — **§8/§9**: the VM-execution baseline
   production path (`benchmark.yml`, D-309) and the two-axis gate (`Grob.BenchCheck`,
   D-313). This is the fourth VM-execution baseline (after Sprints 3, 4, 5).
3. The prior close-gate scripts for shape: Sprint 3's `hello.grob`, Sprint 4's
   `calculator.grob`, Sprint 5's `functions.grob`. `types.grob` follows their form — a
   planning-defined smoke test, **not** one of the thirteen release-gate scripts, using
   no stdlib modules (none exist until Sprint 8).

> **Verify before relying on cited sections.** Grep §7 for the exact acceptance wording
> and `grob-benchmarking-strategy.md` for the baseline-production and two-axis-gate
> rules. If the acceptance or the gate has moved, surface it.
>
> **`types.grob` — the close-gate script.** Built from the Sprint 1–6 surface only, no
> stdlib modules. It must exercise the whole §7 surface end-to-end:
>
> - A **declared type with field defaults**, constructed both ways (a defaulted field
>   supplied, and omitted so its default fills).
> - **Nested construction** (a type whose field is another constructed type).
> - **Field access and assignment** (`instance.field`, `instance.field = v`) and a
>   **nested access** (`a.b.c`).
> - A **recursive type** that the §17.1 walk accepts — `Tree { children: Tree[] }` or
>   `Node { next: Node? }` — built and traversed.
> - A **`#{ }` projection** through `.select()` (the §7 acceptance vehicle), with
>   type-safe field access on the projected elements.
> - A **closure stored in a struct field that escapes its enclosing function** and is
>   called through the field afterwards (the D-325 route, exercised end-to-end through
>   `grob run`, not only in a VM unit test).
>
> Keep it readable and self-checking — print results the acceptance can diff, no `fs`,
> `json`, `process` or any other module.
>
> **Sequencing note.** This is Increment E, the close: A → B → C → D → **E**. No new
> language surface — this increment is the smoke script, the acceptance run and the
> benchmark baseline.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the `types.grob` script, the
   acceptance run, and the benchmark baseline production and gate check — and wait for
   Chris's approval.
2. On approval, run `/start-branch` and propose `feat/types-close`. Wait for Chris.
3. Build `types.grob`, run it, confirm the §7 acceptance, produce the fourth
   VM-execution baseline via the benchmark workflow and confirm it passes the two-axis
   gate (D-313). Follow the `tdd-cycle` skill for any test scaffolding.
4. Run `/commit-message` against the staged changes.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–5** and **Sprint 6 Increments A–D**: the full user-defined-type surface —
  declarations and registry, construction and defaults, field access and assignment,
  anonymous structs and projections. All green coming into the close.

## Deliverable for this increment

The sprint close. **No new language surface.**

1. **`types.grob`.** The close-gate script exercising the whole §7 surface (the bullets
   above), using no stdlib modules, with self-checking output.
2. **§7 acceptance run.** `grob run types.grob` produces the expected stdout; the
   acceptance criteria are met across the merged A–E work. `calculator.grob`,
   `functions.grob` and `hello.grob` still run (no regression of the earlier gates).
3. **Fourth VM-execution benchmark baseline.** Produced via the benchmark workflow
   (D-309) and passing the two-axis gate (`Grob.BenchCheck`, D-313). Commit the JSON
   baseline.

This is the close: no new language surface, no new opcodes and no new error codes. If the
close work surfaces a genuine gap — a missing node, an opcode, a code — that is a
stop-and-propose via `extending-the-grammar`, `adding-an-opcode` or
`allocating-an-error-code`, not a silent edit folded into the close.

## Out of scope

Any new language feature. The long-form per-code docs (`docs/errors/Exxxx.md`) remain a
separate scheduled session. The §7-to-A–E documentation rewrite is a documentation-
authority call deferred to the close discussion, as for §5 and §6 — note it, do not
perform it unless Chris directs.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Integration tests (`Grob.Integration.Tests`):**
  - `types.grob` runs end-to-end and produces the expected stdout (the acceptance
    fixture).
  - The closure-in-field escape in `types.grob` produces the correct value through
    `grob run` (the D-325 route end-to-end, complementing C's VM unit test).
  - `calculator.grob`, `functions.grob` and `hello.grob` still run unchanged.
- **Benchmark:** the VM-execution category runs, the baseline is produced from the
  workflow, and the two-axis gate passes.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `grob run types.grob` runs and meets the §7 acceptance: types declared, constructed
  and accessed; defaults fill omitted fields; nested construction and access resolve;
  undefined field access is a compile error; anonymous structs work in lambdas and
  `select()` projections; the closure-in-field escape runs correctly end-to-end.
- The earlier close-gate scripts still run.
- The fourth VM-execution benchmark baseline is produced from the workflow and passes
  the two-axis gate (D-313).
- The DAG holds; this increment adds no codes — confirm the error-code count is unchanged
  against the **live** registry and the D-316 consistency gate is green, rather than
  asserting a fixed number.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. The close is script-authoring, an acceptance run and a
benchmark baseline over the settled A–D surface. No Opus carve-out.

## Hand-off

Summarise: the `types.grob` script as built and its expected output, the §7 acceptance
result, the earlier-gate regression check and the fourth VM-execution baseline and its
gate result. Note for the next chat: Sprint 6 is closeable — the merged A–E tree is
ready for the GPT-5.3 Codex cold-read against the Sprint 6 QA brief
(`prompts/sprint-6/grob-sprint-6-qa.md`). Sprint 7 is error handling (`try`/`catch`,
the exception hierarchy, `throw`).
