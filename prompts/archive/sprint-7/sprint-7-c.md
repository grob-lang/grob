---
description: "Sprint 7 · Increment C — `finally`. The load-bearing increment. Handler-table finallyOffset for the exceptional path (D-275) + compiler-emitted finally chains for the non-exceptional paths (D-332), innermost-to-outermost across nested try/finally on every early return/break/continue, each exactly once. E2206, E2207. Throw-in-finally replaces the in-flight exception. Opus subagent carve-out on the finally-emission-chain sub-problem."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Task
model: sonnet
---

# Sprint 7 · Increment C — `finally`

This is the load-bearing increment of Sprint 7. Increments A and B built raise and
recover; this increment builds **unconditional cleanup**. A `finally` block runs on
every exit from the try region — normal completion, an uncaught exception propagating
past it, a caught exception after the handler completes or throws, and an early
`return`/`break`/`continue` from inside the try or a catch — and never on `exit()`.
The mechanism is the one new structural decision of the sprint (D-332): the closed
`OpCode` enum has no `Leave`/`EndFinally`, so the exceptional path uses the
handler-table `finallyOffset` the VM already knows how to run (D-275), and the
**non-exceptional** paths are **compiled** — the compiler emits each pending finally
body inline before every control transfer that leaves its region, innermost to
outermost, each exactly once.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 7 — Error Handling** scope
   (the `finally` bullets), §3.3 (confirm the exception opcodes; **no `Leave`/
   `EndFinally` exists and none is added — D-332**), §3.5.
2. `docs/design/grob-language-fundamentals.md` — **§27** `finally` subsection in full:
   when it runs (the six paths), the `exit()` exclusion, the exception-inside-`finally`
   rule, and the control-flow-in-`finally` ban. Read the "When it runs" list and the
   "Control flow inside `finally`" paragraph closely — they are the acceptance surface.
3. `docs/design/grob-grobc-format.md` — the handler-table section; confirm the entry
   already carries (or gains, per D-275) a `finallyOffset` field and how it is
   encoded.
4. `docs/design/grob-error-codes.md` — confirm **E2206** (`finally` not last in
   `try`) and **E2207** (`return`/`break`/`continue` inside `finally`) exist with
   their descriptors. No new code is expected this increment — confirm.
5. Decisions: confirm **D-275** (the `finally` semantics, the six paths, the
   `finallyOffset` handler-table field, throw-in-finally replaces the in-flight
   exception, control-flow-in-finally banned), **D-332** (the finally compilation
   model — the number confirmed next-free against the live log, provisionally D-332,
   extends D-275), **D-110** (`exit()`/`ExitSignal` uncatchable, no finally on that
   path), **D-276** (a `return` in a block-body lambda nested inside `finally` exits
   only the lambda — not banned), **D-315** (`break`/`continue` in `select`) and
   **D-325** (location-based upvalue closing on unwind). Grep the log.

> **Verify before relying on cited decisions and sections.** Grep to confirm §27's
> `finally` subsection, the handler-table `finallyOffset` and D-275/D-332 read as this
> prompt assumes. If a section has moved or a decision been superseded, surface it
> rather than proceeding. **If the live decisions log has no D-332 yet, allocate it at
> the next-free number** (verify the tail; do not take 332 from this prompt) as the
> record of the compilation model below, three-location lockstep, extending D-275 —
> this is the decision this increment implements and it must be logged with the code.
>
> **`finally` rules — inline reference (authoritative source is §27, D-275 and D-332;
> reproduced here so the implementation does not depend on a fetch landing well).**
>
> - **The six paths it runs on.** normal completion of the try block; an uncaught
>   exception propagating past the try; a caught exception after the catch handler
>   completes normally; a caught exception after the catch handler itself throws
>   (original or rethrown); early `return` from inside the try (or a catch); early
>   `break`/`continue` from inside the try (or a catch). It runs **exactly once** on
>   whichever path is taken.
> - **The one path it never runs on.** `exit()`/`ExitSignal` (D-110) terminates the
>   process unconditionally without running any finally. This is a VM-level stack
>   teardown, not a control transfer — the compiler does not chain it and the VM does
>   not run `finallyOffset` for it.
> - **The compilation model (D-332). This is the crux — get it exactly right.**
>   - **Exceptional path — VM-run.** The handler-table entry carries a `finallyOffset`
>     (D-275). When an exception propagates out of a region (uncaught from the try, or
>     thrown from a catch handler), the VM runs the finally body at `finallyOffset`
>     **before** propagating to the next enclosing region. This path is the VM's.
>   - **Non-exceptional paths — compiler-emitted.** The closed enum has no
>     `Leave`/`EndFinally`, so the VM cannot intercept a `Return`/`Jump` to run a
>     pending finally. The **compiler** tracks the stack of enclosing `try`/`finally`
>     regions. At every control-transfer site that leaves one or more of them — normal
>     fall-through completion of a try body **and** of a catch body, and an early
>     `return`/`break`/`continue` from inside a try or catch body — it emits each
>     pending finally block's body inline, **innermost-to-outermost**, before the
>     transfer instruction. Normal completion emits the finally once at the
>     fall-through. A non-local exit through *N* nested `try`/`finally` regions emits
>     *N* finally bodies, each **exactly once**, in order.
>   - **The partition is exact.** Compiler-emitted finally on every straight-line
>     fall-through and early-exit path; VM-run finally (`finallyOffset`) on every
>     exceptional propagation path. A finally body's bytecode may therefore appear at
>     several sites (the classic javac-post-`jsr` duplication) — that is expected and
>     correct.
> - **The E2207 ban makes the chain analyzable.** `return`/`break`/`continue` inside
>   a `finally` body are compile errors (**E2207**). A finally body therefore never
>   itself transfers control, so every emitted chain is straight-line and terminating.
>   The single finally-body exit that is not straight-line is a `throw`, which is
>   permitted and re-enters the unwinding path.
> - **Throw inside `finally` replaces the in-flight exception (D-275).** If a finally
>   body throws while an exception is already in flight (propagating from the try or a
>   catch), the new exception replaces the original — the original is lost (no
>   exception chaining in v1). This holds identically whether the finally is reached
>   on the VM-run exceptional path or emitted on a non-exceptional path that a throw
>   then diverts.
> - **`finally` grammar.** Optional; if present, exactly once, and the **last** clause
>   of the `try` — a `finally` not last is **E2206**. A `try` with only a `finally`
>   (no catches) is legal. A `try` with neither catch nor finally is E2204 (B).
> - **The lambda exception (D-276).** The E2207 ban is on control flow that exits the
>   enclosing function or loop. A `return` inside a **block-body lambda** that appears
>   inside a `finally` exits only the lambda and is permitted — the lambda is a
>   function body in its own right. Do not flag it as E2207.
> - **Unwind respects D-325.** A finally running on an exceptional path, and the
>   frames torn down around it, close open upvalues by location exactly as a normal
>   return does. A closure captured before a `throw` and reached after the finally
>   runs must see its captured value with no value-stack underflow. Mandatory
>   acceptance line.
>
> **Sequencing note.** This is Increment C: A → B → **C (finally)** → D (runtime-throw
> catchability + uncatchable exit) → E (close). Do not pull the runtime-site routing
> or the smoke script forward. This increment is `finally` only.

## The Opus subagent carve-out

The finally-emission-chain compilation is the one load-bearing sub-problem of the
sprint: tracking the enclosing-region stack, emitting the correct pending-finally
chain innermost-to-outermost at every control-transfer site (try fall-through, catch
fall-through, `return`, `break`, `continue`), guaranteeing each finally runs exactly
once on every path, and getting the interaction with `break`/`continue` targeting an
**outer** loop right when intervening `try`/`finally` regions sit between the transfer
and its target. Drive the increment on Sonnet 4.6 (High), but **escalate the
finally-emission-chain sub-problem to the Opus 4.8 subagent** — the Sprint 5 D
`grob-closure-specialist` mechanism. Add the subagent config under `.claude/agents/`
(a `grob-unwind-specialist`, scoped to the compiler's control-transfer emission and
the VM's `finallyOffset` arm) as part of this increment. Everything else — the E2206/
E2207 checks, the grammar wiring, the tests — is Sonnet's. This is the sprint's one
sanctioned Opus reach; do not reach for it on the mechanical parts.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered implementation plan — the enclosing-region
   stack in the compiler, the pending-finally-chain emission at each transfer site,
   the `finallyOffset` VM arm, the E2206/E2207 checks, throw-in-finally replacement,
   the D-276 lambda carve-out, the D-332 decision-log entry and the tests — and wait
   for Chris's approval before editing. The plan is the gate. Flag in the plan where
   the Opus subagent is engaged.
2. On approval, run `/start-branch` and propose `feat/finally`. Wait for Chris to
   create the branch.
3. Implement with TDD — tests written and confirmed **failing** first. The
   exactly-once and innermost-to-outermost properties are test-first, not
   test-after. Follow the `tdd-cycle` skill.
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–6** as summarised in the Increment A prompt.
- **Sprint 7 A:** the exception hierarchy, `throw`, the unhandled top-level path.
- **Sprint 7 B:** `try`/`catch` — `TryBegin`/`TryEnd`, the handler table, polymorphic
  source-order matching, the catch-all-last rule, the immutable catch binding, the
  catch compile errors (E2204, E2205, the catch-type and duplicate-catch codes) and
  the VM unwind-to-nearest-handler arm. All green.

## Deliverable for this increment

`finally` running correctly on every normal and exceptional path and never on
`exit()`, with the D-332 compilation model.

1. **The `finallyOffset` VM arm (exceptional path).** The handler-table entry carries
   `finallyOffset`; the VM runs the finally body before propagating an uncaught or
   catch-thrown exception past the region.
2. **The emitted finally chains (non-exceptional paths).** The compiler tracks the
   enclosing-region stack and emits each pending finally body innermost-to-outermost
   before every control transfer that leaves its region — try fall-through, catch
   fall-through, and early `return`/`break`/`continue` — each exactly once. Correct
   for `break`/`continue` whose target loop sits outside one or more `try`/`finally`
   regions.
3. **E2206 and E2207.** `finally` not last is E2206; `return`/`break`/`continue`
   inside a `finally` body is E2207, with the D-276 lambda carve-out honoured. Both
   through `ErrorCatalog` descriptors.
4. **Throw-in-finally.** A throw from inside a finally replaces any in-flight
   exception; the original is lost (no chaining).
5. **The `exit()` exclusion.** `exit()`/`ExitSignal` unwinds past finally without
   running it — no `finallyOffset` run, no emitted chain reached.
6. **D-325 on the finally path.** Upvalues close by location on frames torn down
   around a finally; no value-stack underflow.
7. **The D-332 decision-log entry**, logged at the next-free `D-###` in three-location
   lockstep, extending D-275, recording the compilation model. No new error code this
   increment — confirm the count is unchanged.

No new opcodes (the enum stays closed — **no `Leave`/`EndFinally`**), no parser/AST
edits.

## Out of scope

Routing the existing runtime throw sites through the exception model and the
uncatchable-`exit()` verification across the runtime surface (Increment D — this
increment builds the `exit()`-skips-finally behaviour, but D is where every runtime
throw site is confirmed catchable). The smoke script and the benchmark (Increment E).
Do not add a `Leave`/`EndFinally` opcode — if you believe you need one, stop and
surface: D-332 is precisely the decision that the compiler handles the non-exceptional
paths so the enum stays closed.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker / parser tests (`Grob.Compiler.Tests`):**
  - E2206 on a `finally` before a `catch`; E2207 on `return`, on `break` and on
    `continue` inside a `finally`; a `return` inside a block-body lambda inside a
    `finally` is **not** E2207 (D-276). A `try { } finally { }` with no catch
    type-checks.
- **Compiler / disassembler tests (`Grob.Compiler.Tests`):**
  - The finally body is emitted at the try fall-through, at each catch fall-through,
    and before each early `return`/`break`/`continue` leaving the region — verified
    through the disassembler; the handler-table entry carries the `finallyOffset`.
  - A `return` through two nested `try`/`finally` regions emits both finally bodies,
    inner then outer, before the return — verified through the disassembler.
- **VM tests (`Grob.Vm.Tests`) — the acceptance surface:**
  - `finally` runs on normal completion; on an uncaught exception (then the exception
    continues); after a catch completes normally; after a catch throws (then the new
    exception propagates); before an early `return`; before an early `break` and
    `continue`. Each runs the finally **exactly once** (assert via a counter the
    finally increments).
  - Nested `try`/`finally`: an early `return` through *N* regions runs all *N*
    finallys, inner-to-outer, each once; a `break` targeting an outer loop through an
    intervening `try`/`finally` runs the intervening finally once then breaks.
  - Throw-in-finally replaces the in-flight exception (the outer catch sees the
    finally's exception, not the original).
  - `exit()` inside `try`/`finally` runs **no** finally and terminates with the exit
    code (D-110).
  - A closure captured before a `throw` and reached after the finally runs sees its
    captured value with no value-stack underflow (D-325).
  - Layer-invariant `[Theory]` rows: pathological but parseable `try`/`finally` shapes
    execute to a result or a diagnostic, never throw a host exception or underflow.
- **Integration / spec-consistency:** the consistency gate (D-316) is green; the
  error-code count is unchanged (C adds none); the D-332 entry is present and in
  lockstep.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `finally` runs exactly once on each of the six paths and never on `exit()`.
- Nested `try`/`finally` early exits run all pending finallys innermost-to-outermost,
  each exactly once; `break`/`continue` through intervening regions is correct.
- E2206 and E2207 fire correctly, with the D-276 lambda carve-out; throw-in-finally
  replaces the in-flight exception.
- Upvalues close by location on the finally path; no value-stack underflow.
- No new opcodes (no `Leave`/`EndFinally`), no parser/AST edits; the DAG holds.
- The D-332 entry is logged in three-location lockstep, extending D-275; the
  error-code count is unchanged.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) drives the increment. The **finally-emission-chain sub-problem** —
the enclosing-region stack, the innermost-to-outermost chain at every transfer site,
the exactly-once guarantee and the outer-loop-target interaction — escalates to the
Opus 4.8 subagent (`grob-unwind-specialist`, added under `.claude/agents/` this
increment), the Sprint 5 D `grob-closure-specialist` mechanism. This is the sprint's
one sanctioned Opus reach; the E2206/E2207 checks, the grammar wiring and the tests
stay on Sonnet.

## Hand-off

Summarise: the enclosing-region stack and the pending-finally-chain emission as built,
the `finallyOffset` VM arm, how the exceptional/non-exceptional partition maps to
VM-run vs compiler-emitted, the E2206/E2207 checks and the D-276 carve-out, the
throw-in-finally replacement and the `exit()` exclusion, the D-332 entry as logged,
and the test files added — especially the exactly-once and nested-chain tests. Note for
the next chat: Increment D routes the existing runtime throw sites (ArithmeticError,
NilError, IndexError, RuntimeError) through the exception object model so `try`/`catch`
catches them, clarifies the D-039 two-mode contract to "first **unhandled** runtime
error", and verifies `exit()` is uncatchable across the whole runtime surface.
