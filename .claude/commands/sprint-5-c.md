---
description: "Sprint 5 · Increment C — lambdas and natives. The three lambda forms, block-body implicit-last-expression with return early-exit (D-276), NativeFunction + RegisterNative, the native↔VM call-back bridge, and filter/select/sort/each as the lambda-consuming array natives. Top-level reference resolution only (D-296 categories 1–3) — no upvalue capture. Plus the mechanical §6 map → select fix, and the D-319 cooperative-cancellation seam: a CancellationToken on the VM run entry and a masked step-budget check in the dispatch loop, the counter on the VM instance so the budget spans the re-entrant native↔VM bridge."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 5 · Increment C — lambdas and natives

Increment B completed named functions. This increment adds **anonymous functions
as values** — the three lambda forms — and the **native-function machinery** they
flow through: `NativeFunction`, `RegisterNative`, the bridge that lets a C# native
call back into the VM, and the array higher-order methods `filter`/`select`/`sort`/
`each` as the natives that consume a lambda. Lambdas here resolve only **top-level**
references (D-296 categories 1–3 — `const` inlined, `readonly`/mutable read from
the globals table). **Upvalue capture of enclosing-function locals (category 4) is
Increment D** — do not build it here. This increment also makes the mechanical §6
`map` → `select` correction, and folds in one cross-cutting VM-core change from
**D-319**: a cooperative-cancellation **step-budget seam** in the dispatch loop.
It lands here, not elsewhere, because this increment builds the re-entrant native↔VM
call-back bridge — so the seam's load-bearing property (one budget that spans
re-entrant execution, catching a runaway lambda invoked by a native) is testable
exactly where the bridge is built. The seam is mechanism only; it is not lambda or
native logic.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 5** scope (the lambda,
   `BytecodeFunction`/`NativeFunction`, `RegisterNative` and "lambdas work in
   filter/select/sort" bullets), §3.5 (test routing). Note the acceptance line
   still reads "filter, **map**, sort" — see the §6 correction below.
2. `docs/design/grob-language-fundamentals.md` — **§12.1** (closure variable
   resolution — the four categories; this increment implements **1–3 only**), the
   lambda grammar (`x => expr`, `x => { block }`, `(a, b) => expr`), and the
   block-body rule (implicit last-expression result, `return` for early exit,
   D-276). Grep for the four-category classification and the block-body rule.
3. `docs/design/grob-type-registry.md` — the `T[]` higher-order methods:
   `filter(fn: T → bool)`, `select<U>(fn: T → U)`, `sort<U: Comparable>(fn: T → U,
   descending: bool = false)`, `each(fn: T → void)`. Confirm the signatures;
   `select` is the transformation primitive (D-280 removed `map`).
4. `docs/design/grob-vm-architecture.md` — how the VM dispatches a call where the
   callee is a native rather than a `BytecodeFunction`, and the constraint that a
   native may call back into the VM (re-entrant execution) to invoke a lambda
   argument.
5. Decisions: **D-115** (lambdas and closures), **D-276** (block-body lambda
   return), **D-296** (four-category resolution), **D-280** (`select` replaces
   `map`), **D-281** (`sort` key-selector), and **D-319** (the playground embedding
   host — the dispatch-loop cancellation/step-budget seam, adopted into v1 and
   folded into this increment). Grep the log; do not take their content from this
   prompt.

> **Verify before relying on cited decisions and sections.** Grep
> `grob-language-fundamentals.md` §12.1 and the lambda grammar, and
> `grob-type-registry.md` for the `T[]` method signatures. If §12.1's
> categorisation or a method signature reads differently than this prompt assumes,
> surface it rather than proceeding.
>
> **Lambda and native rules — inline reference (authoritative source is
> `grob-language-fundamentals.md` §12.1, `grob-type-registry.md` and the cited
> decisions; reproduced here so the implementation does not depend on a fetch
> landing well).**
>
> - **Three lambda forms.** `x => expr`, `x => { block }`, `(a, b) => expr`. The
>   block body (`x => { ... }`) returns its **implicit last expression**; `return`
>   inside a block-body lambda is an **early exit from the lambda only** (D-276),
>   not the enclosing function. `{ }` after a lambda arrow is **always** a block
>   body; `#{ }` is an anonymous struct (no parser ambiguity — already settled,
>   Sprint 6 territory, do not build struct construction here).
> - **Lambda as a value.** A lambda compiles to a `BytecodeFunction` and is a
>   first-class `GrobValue` of the `Function` variant (the variant already exists,
>   D-303). It can be bound (`f := x => x + 1`), passed as an argument and called.
> - **Four-category resolution — categories 1–3 only this increment (D-296).** The
>   compiler classifies each identifier referenced in a lambda body:
>   1. **Top-level `const`** — inlined as a constant-pool load. No runtime slot.
>   2. **Top-level `readonly`** — global read (the value never changes, but
>      mechanically it is a global-read opcode).
>   3. **Top-level mutable** — global read, and global write from the lambda body.
>   4. **Enclosing-function local** — **upvalue capture. NOT this increment.** If a
>      lambda body references an enclosing-function local, that is Increment D's
>      work; here, resolve only categories 1–3. A lambda that would need a category-4
>      capture is out of scope — do not emit `Closure`/`GetUpvalue`/`SetUpvalue`/
>      `CloseUpvalue`. (A top-level lambda, or a lambda whose only free references
>      are top-level, never needs capture — that is the bulk of the sample usage.)
> - **`NativeFunction` and `RegisterNative`.** A native is a function whose body is
>   C#, not bytecode. `RegisterNative(name, signature, implementation)` registers
>   one so the type checker validates calls against its signature at compile time
>   and the VM dispatches it. The VM's `Call` arm must dispatch `BytecodeFunction`
>   and `NativeFunction` **transparently** — same call syntax, different execution.
> - **The native↔VM call-back bridge.** `filter`/`select`/`sort`/`each` each take a
>   lambda argument and invoke it once per element. The native receives the lambda
>   as a callable `GrobValue` and must call **back into the VM** to run it. Build
>   this bridge — a native invoking a Grob `BytecodeFunction`/closure — carefully:
>   it is re-entrant VM execution, and the frame/stack discipline from Increment A
>   must hold across the boundary. This is the load-bearing piece of this
>   increment.
> - **The array natives.** Implement `filter(fn: T → bool) → T[]`,
>   `select<U>(fn: T → U) → U[]`, `sort<U: Comparable>(fn: T → U, descending: bool
>   = false) → T[]` (stable, key-selector only, D-281), `each(fn: T → void) → void`.
>   `filter`/`select`/`sort` return new arrays and never mutate; `sort` is stable.
>   Confirm assembly ownership (these are stdlib-surface natives, not compiler or VM
>   types — decide the home against the DAG before adding).
> - **No new opcodes.** Lambdas-without-capture compile to a `BytecodeFunction` and
>   a normal `Call`; natives dispatch through the same `Call` arm. No
>   `Closure`/upvalue opcode is emitted in this increment.
> - **Cooperative cancellation / step budget (D-319) — VM-core, not lambda logic.**
>   The VM run entry takes a `CancellationToken` (default `CancellationToken.None`,
>   which means unlimited — production wires that). The dispatch loop checks it on a
>   masked interval (`if ((++_steps & BudgetMask) == 0) token.ThrowIfCancellationRequested();`)
>   so a runaway script can be interrupted; in steady state the cost is a counter
>   increment and an occasional predictable branch. The step counter lives on the
>   **VM instance**, not per-`Run` and not per-frame, so the budget is **continuous
>   across the re-entrant call-back bridge** — a runaway lambda invoked by a native
>   (`arr.each(x => while (true) {})`) is caught, not just a runaway top-level loop.
>   Cancellation surfaces as `OperationCanceledException` — a .NET exception
>   **outside the `GrobError` hierarchy** — so a Grob `catch e` (D-274) cannot
>   swallow it, the same uncatchable property `ExitSignal` has for `exit()`. **No
>   new opcode, no new error code, no parser or AST edit.** The seam is the
>   mechanism only — it is not a Grob-level `timeout` API and not `process.run`'s
>   timeout; the host decides the cancellation policy (manual, a wall-clock
>   `CancellationTokenSource`, or a step cap).
>
> **The §6 `map` → `select` correction.** `grob-v1-requirements.md` §6 (Sprint 5
> acceptance) reads "Lambdas work in filter, **map**, sort". `map` was removed by
> **D-280**; the canonical transformation method is `select`. Correct the §6
> acceptance prose to "filter, **select**, sort". This is a mechanical drift fix
> sanctioned in planning (the registry and `writing-grob-source` already say
> `select`); it is not a design change. Make the one-word edit; do not restructure
> §6.
>
> **Sequencing note.** This is Increment C: A → B → **C (lambdas + natives)** → D
> (closures) → E (init machine + narrowing) → F (close). Do not pull upvalue
> capture, the init machine or narrowing forward. If a lambda body needs a
> category-4 capture, that is the signal you are pulling D forward — stop.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the lambda compilation
   (`BytecodeFunction`, categories 1–3 resolution, block-body return), the
   `NativeFunction`/`RegisterNative` surface and its assembly home, the native↔VM
   call-back bridge, the four array natives, the D-319 cancellation seam, the §6
   one-word fix and the tests — and wait for Chris's approval. Name the call-back
   bridge explicitly as the load-bearing sub-problem.
2. On approval, run `/start-branch` and propose `feat/lambdas-and-natives`. Wait
   for Chris to create the branch.
3. Implement with TDD — tests written and confirmed **failing** first (follow
   `tdd-cycle`). New `.grob` fixtures follow `writing-grob-source`.
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–4** and **Sprint 5 A–B**: the call frame, recursion, the E5901
  guard, `BytecodeFunction`, positional and named/default calls, E0003/E0004/E0005
  and the registered E0008–E0011. All green.

## Deliverable for this increment

Lambdas as values, the native-function machinery, the native↔VM call-back bridge
and the four array higher-order methods — top-level reference resolution only.
**No upvalue capture, no init machine, no narrowing.**

1. **Compiler — lambdas.** Compile each lambda form to a `BytecodeFunction`;
   classify each free identifier into D-296 categories **1–3** and emit the
   constant-pool load / global read / global write accordingly. Block-body lambdas
   return the implicit last expression; `return` is a lambda-local early exit
   (D-276).
2. **Runtime — `NativeFunction` + `RegisterNative`.** Add the native
   representation and the registration surface; register the signatures with the
   type checker for compile-time validation. Decide the assembly home against the
   DAG.
3. **VM — transparent dispatch + call-back bridge.** The `Call` arm dispatches
   `BytecodeFunction` and `NativeFunction` transparently. Build the re-entrant
   bridge that lets a native invoke a lambda argument back through the VM, holding
   the frame/stack discipline across the boundary.
4. **Stdlib surface — the four array natives.** `filter`, `select`, `sort` (stable,
   key-selector), `each` on `T[]`, each invoking its lambda through the bridge.
   `filter`/`select`/`sort` return new arrays; none mutate.
5. **Spec — §6 correction.** Change "filter, map, sort" to "filter, select, sort"
   in the Sprint 5 acceptance. One-word mechanical fix, no restructure.
6. **Diagnostics via `ErrorCatalog` (D-308).** Argument-type mismatches on a
   lambda's parameter or a native's signature reuse **E0004**; no new codes are
   expected in this increment. If one seems needed, stop and surface.
7. **VM — cooperative-cancellation step-budget seam (D-319).** Add a
   `CancellationToken` to the VM run entry (default `CancellationToken.None`) and a
   masked step-budget check in the dispatch loop. The step counter is a **VM-instance
   field**, so the budget spans the re-entrant call-back bridge built in item 3 —
   a runaway lambda invoked by a native is caught, not only a runaway top-level
   loop. Cancellation surfaces as `OperationCanceledException` (outside `GrobError`,
   uncatchable by Grob `catch`). Production wires the unlimited default; the seam is
   the mechanism, not a policy. No new opcode, no new error code.

No new opcodes, no parser or AST edits.

## Out of scope

Upvalue capture of enclosing-function locals — D-296 category 4 — and the
`Closure`/`GetUpvalue`/`SetUpvalue`/`CloseUpvalue` opcodes (Increment D). The
top-level initialisation state machine and flow-sensitive narrowing (Increment E).
Anonymous struct construction (`#{ }`) and `select()` projections returning
anonymous structs (Sprint 6). Multi-parameter lambdas have grammar support but no
v1 stdlib native consumes one (`sort` is key-selector) — do not invent a native
that does. Do not edit the parser, the AST or the `OpCode` enum. For the D-319
seam: build the mechanism only — the `CancellationToken` on the run entry and the
masked dispatch-loop check. Do not add a Grob-level `timeout`/`deadline` language
surface, a `process.run` timeout, or any cancellation policy; production wires the
unlimited default.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - Each lambda form type-checks; a lambda's `ResolvedType` is its function type;
    arms of a lambda body and the block-body implicit return resolve.
  - A lambda referencing a top-level `const` compiles to a constant-pool load; a
    top-level `readonly`/mutable reference compiles to a global read; a write to a
    top-level mutable from a lambda body compiles to a global write (categories
    1–3, verified through the disassembler).
  - A lambda passed to `filter`/`select`/`sort`/`each` type-checks against the
    method signature; a wrong lambda return type (e.g. a non-`bool` `filter`
    predicate) is **E0004**.
  - §3.1.1 invariant holds for every identifier node introduced.
  - Layer-invariant `[Theory]` rows over pathological lambda/native-call inputs.
- **VM tests (`Grob.Vm.Tests`):**
  - A hand-built chunk calling a `NativeFunction` dispatches and returns correctly.
  - The call-back bridge: a native that invokes a lambda argument per element
    produces the right result and leaves the stack at the expected depth (the
    re-entrant frame discipline holds).
  - **Cancellation (D-319):** a runaway top-level loop (`while (true) {}`) run with a
    token that cancels terminates with `OperationCanceledException` rather than
    hanging, and a Grob `try/catch` around it does **not** swallow the cancellation.
  - **Cancellation spans the bridge (D-319):** a runaway lambda invoked through a
    native (`arr.each(x => while (true) {})`) is also cancelled — proving the budget
    counter is on the VM instance, not per-`Run` or per-frame. The unlimited default
    (`CancellationToken.None`) never cancels a terminating program.
- **Integration tests (`Grob.Integration.Tests`):**
  - A script using `arr.filter(...).select(...).sort(...)` with top-level-referencing
    lambdas runs end-to-end and produces the expected stdout (`select`, not `map`).
  - `each` runs a side-effecting lambda over an array in order.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- The three lambda forms work as values; block-body lambdas return their implicit
  last expression with `return` as a lambda-local early exit; categories 1–3
  resolve correctly; **no** upvalue opcode is emitted.
- `NativeFunction`/`RegisterNative` exist; the VM dispatches natives transparently;
  the call-back bridge runs a lambda argument re-entrantly with correct frame
  discipline; `filter`/`select`/`sort`/`each` work.
- The D-319 cancellation seam works: a runaway loop and a runaway lambda-through-a-
  native both terminate under a cancelling token via `OperationCanceledException`
  (outside `GrobError`, uncatchable by Grob `catch`); the unlimited default never
  cancels a terminating program; no new opcode, no new error code.
- §6 reads "filter, select, sort"; the consistency gate (D-316) stays green.
- The §3.1.1 invariant holds; no new opcodes; the DAG holds (decide the native
  home in `Grob.Stdlib`/`Grob.Runtime`, never an upward reference).
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. Lambdas-without-capture and native dispatch are
specified machinery; the one genuinely fiddly piece is the re-entrant call-back
bridge, which is structural but not category-4 capture — if it gets thorny, that
is a `/model opus` moment within this session, not the Increment D closure
subagent. There is no closure carve-out in this increment.

## Hand-off

Summarise: the lambda compilation and how categories 1–3 resolve, the
`NativeFunction`/`RegisterNative` surface and its assembly home, the native↔VM
call-back bridge as built and the frame discipline across it, the four array
natives, the D-319 cancellation seam (where the step counter lives and how it spans
the bridge, confirmed by the runaway-lambda-through-a-native test), and the §6
correction. Note for the next chat: Increment D is closures —
category-4 upvalue capture (`GetUpvalue`/`SetUpvalue`/`CloseUpvalue`/`Closure`),
open upvalues over stack slots, closed upvalues to the heap on the enclosing
function's return, independent capture per call. D is the Opus carve-out
(`grob-closure-specialist`).
