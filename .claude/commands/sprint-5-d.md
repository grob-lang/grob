---
description: "Sprint 5 · Increment D — closures. Category-4 upvalue capture: GetUpvalue/SetUpvalue/CloseUpvalue/Closure emission and dispatch, open upvalues over live stack slots, closed upvalues copied to the heap on the enclosing function's return, the Closure runtime object, independent capture per enclosing-function call. The clox closures chapter — the heaviest increment, and the only Opus carve-out (grob-closure-specialist)."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 5 · Increment D — closures

Increment C made lambdas first-class values that resolve top-level references
(D-296 categories 1–3). This increment adds the one category C deferred: **capture
of an enclosing-function local** (category 4) as an **upvalue**. This is the clox
closures chapter — open upvalues referencing live stack slots, closed upvalues
copied to the heap when the enclosing function returns, the `Closure` runtime
object, and independent capture per enclosing-function call. It is the heaviest
increment of the sprint and the only one with an Opus carve-out.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 5** scope (the closures
   bullet: `GetUpvalue`/`SetUpvalue`/`CloseUpvalue`/`Closure`, open vs closed
   upvalues), §3.3 (confirm the four upvalue opcodes are present in the closed
   enum), §3.5 (test routing).
2. `docs/design/grob-language-fundamentals.md` — **§12.1** (the four categories;
   this increment implements **category 4**, capture). Grep for the `makeCounter`
   example and the "capture applies only to category 4" rule and the
   independent-capture-per-call rule.
3. `docs/design/grob-vm-architecture.md` — the upvalue mechanism: open upvalues as
   references into the value stack, the close-on-return copy to the heap, and the
   `Closure` object as a `BytecodeFunction` plus its upvalue array.
4. Decisions: **D-296** (closure capture, the four categories), **D-115** (lambdas
   and closures — the clox upvalue design). Grep the log; do not take their
   content from this prompt.

> **Verify before relying on cited decisions and sections.** Grep
> `grob-language-fundamentals.md` §12.1 for the category-4 rule and the
> independent-capture example, and `grob-vm-architecture.md` for the open/closed
> upvalue mechanism. If either reads differently than this prompt assumes, surface
> it rather than proceeding.
>
> **Closure rules — inline reference (authoritative source is
> `grob-language-fundamentals.md` §12.1, `grob-vm-architecture.md` and D-296;
> reproduced here so the implementation does not depend on a fetch landing well).**
>
> - **What is captured (category 4 only, D-296).** Only an **enclosing-function
>   local** referenced from a nested lambda body is captured, as an upvalue.
>   Top-level references (categories 1–3) are **not** capture — C already resolves
>   them and this increment leaves that path unchanged. "Capture" means category 4.
> - **The `Closure` object.** A capturing lambda becomes a `Closure` at runtime — a
>   `BytecodeFunction` plus an **upvalue array**. The `Closure` opcode creates it,
>   reading the enclosing frame's slots / parent upvalues to populate the array.
> - **Open vs closed upvalues.** While the enclosing function is still on the
>   stack, an upvalue is **open** — it references the live stack slot, so reads and
>   writes through `GetUpvalue`/`SetUpvalue` see the same variable the enclosing
>   function sees. When the enclosing function **returns**, each upvalue that
>   points at one of its slots is **closed** — the value is copied off the stack to
>   the heap and the upvalue thereafter references the heap copy. `CloseUpvalue`
>   (or the return path) performs the close. After closing, the closure keeps the
>   variable alive independent of the (now gone) enclosing frame.
> - **Independent capture per call.** Each call to the enclosing function produces
>   a closure with its **own** captured slot. Two `makeCounter()` results do not
>   share state — `c1()` and `c2()` count independently. This is the headline test
>   of the increment; if two closures share a counter, the close-on-return or the
>   per-call slot allocation is wrong.
> - **The four opcodes already exist (Sprint 2 A).** `GetUpvalue`, `SetUpvalue`,
>   `CloseUpvalue` and `Closure` are in the closed enum. This increment writes
>   their **compiler emission and VM dispatch arms** — it does not add an enum
>   case. The `Upvalue` runtime type lives in `Grob.Vm` (per the solution
>   architecture); confirm the home against the DAG.
> - **Compiler upvalue resolution.** When a lambda body references a category-4
>   local, the compiler records an upvalue on the lambda's function, resolving
>   whether it captures a **local of the immediately enclosing function** or an
>   **upvalue of the enclosing function** (transitive capture through nested
>   lambdas). Emit `Closure` with the upvalue descriptors; emit `GetUpvalue`/
>   `SetUpvalue` for reads/writes of the captured variable in the body.
>
> **The Opus carve-out.** This increment may invoke the
> **`grob-closure-specialist`** subagent (Opus 4.8) for the one genuinely
> structural sub-problem: the open/closed upvalue lifetime — when exactly an
> upvalue closes, the slot/heap hand-off on return, transitive capture, and the
> per-call independence. Reach for it **only if that interaction gets fiddly**,
> not for routine emission. Routine `GetUpvalue`/`SetUpvalue` emission and the
> `Closure` creation are the main session's Sonnet work. Follow the
> `emitting-closures-and-upvalues` skill.
>
> **Sequencing note.** This is Increment D: A → B → C → **D (closures)** → E (init
> machine + narrowing) → F (close). Do not pull the init state machine or
> narrowing forward. The §19.1 circular-initialisation case is E's, even though it
> involves a top-level reference — D builds capture, E builds the startup tag.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the compiler's upvalue
   resolution (local vs enclosing-upvalue, transitive), the `Closure` emission, the
   `GetUpvalue`/`SetUpvalue` emission, the VM's `Closure`/`GetUpvalue`/`SetUpvalue`/
   `CloseUpvalue` dispatch and the open→closed lifecycle, and the tests — and wait
   for Chris's approval. State explicitly whether you expect to invoke the
   `grob-closure-specialist` subagent and for which sub-problem.
2. On approval, run `/start-branch` and propose `feat/closures`. Wait for Chris to
   create the branch.
3. Implement with TDD — tests written and confirmed **failing** first (follow
   `tdd-cycle`). Follow the `emitting-closures-and-upvalues` skill for the
   open/closed lifecycle. The opcode arms follow `adding-an-opcode`'s
   emit-and-dispatch-together discipline (the enum step is a no-op).
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–4** and **Sprint 5 A–C**: the call frame, recursion, the E5901
  guard, positional and named/default calls, E0003–E0005 and E0008–E0011, lambdas
  as values resolving categories 1–3, `NativeFunction`/`RegisterNative`, the
  native↔VM call-back bridge, and `filter`/`select`/`sort`/`each`. All green.

## Deliverable for this increment

Category-4 upvalue capture across the compiler and the VM — the `Closure` object,
the open/closed upvalue lifecycle and independent per-call capture. **No init
machine, no narrowing.**

1. **Compiler — upvalue resolution.** When a lambda body references a category-4
   local, record an upvalue on the lambda's function — resolving local-of-enclosing
   vs upvalue-of-enclosing (transitive through nested lambdas). Emit `Closure` with
   the upvalue descriptors and `GetUpvalue`/`SetUpvalue` for captured-variable
   reads/writes.
2. **VM — `Closure` / `GetUpvalue` / `SetUpvalue` / `CloseUpvalue` dispatch.**
   Create the `Closure` object (`BytecodeFunction` + upvalue array). Implement open
   upvalues as references into the value stack and closing them — copying to the
   heap — on the enclosing function's return. `GetUpvalue`/`SetUpvalue` read/write
   through the upvalue whether open or closed. Confirm the `Upvalue` type's
   assembly home against the DAG.
3. **Independent per-call capture.** Each enclosing-function call allocates its own
   captured slot; two closures from two calls do not share state.
4. **Diagnostics via `ErrorCatalog` (D-308).** No new codes are expected — capture
   is a resolution/runtime mechanism, not a new error class. If a diagnostic seems
   needed (e.g. a capture the resolver cannot place), stop and surface; do not
   invent.

No new opcodes (the enum is closed), no parser or AST edits.

## Out of scope

The top-level initialisation state machine (§19.1, D-294) and the **E5902**
circular-initialisation diagnostic (Increment E) — even though a top-level lambda
referencing a not-yet-initialised global is the case E guards, the **tag and the
diagnostic** are E's; D builds only category-4 capture. Flow-sensitive narrowing
(Increment E). Do not edit the parser, the AST or the `OpCode` enum. If you reach
to add an opcode, stop and surface.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - A lambda capturing an enclosing-function local records an upvalue and
    disassembles to `Closure` + `GetUpvalue`/`SetUpvalue` (verified through the
    disassembler, not only the VM's answer).
  - Transitive capture — a lambda inside a lambda capturing the outer function's
    local — resolves to an upvalue-of-enclosing chain.
  - A top-level-only lambda (category 1–3) emits **no** `Closure`/upvalue opcode
    (the C path is unchanged — a guard against accidentally routing categories 1–3
    through capture).
  - §3.1.1 invariant holds for every identifier node introduced.
- **VM tests (`Grob.Vm.Tests`):**
  - A `makeCounter`-shaped closure increments its captured local across calls.
  - **Independence:** two closures from two enclosing-function calls count
    independently (the headline correctness test).
  - **Close-on-return:** a closure outlives its enclosing frame — after the
    enclosing function returns, reading/writing the captured variable through the
    closed upvalue works and reflects mutations.
  - A captured variable mutated through `SetUpvalue` is observed by another closure
    capturing the same slot while the enclosing frame is still open.
- **Integration tests (`Grob.Integration.Tests`):**
  - A script with a closure factory (independent counters) and a capturing lambda
    passed to `filter`/`select` runs end-to-end and produces the expected stdout.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- Closures capture enclosing-function locals; open upvalues track the live slot;
  upvalues close to the heap on the enclosing function's return; closures from
  separate calls capture independently.
- Top-level-only lambdas emit no upvalue opcode (categories 1–3 path unchanged).
- The §3.1.1 invariant holds; no new opcodes; the DAG holds (the `Upvalue` type in
  `Grob.Vm`, not shared upward).
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) is the default. The Opus 4.8 **`grob-closure-specialist`**
subagent is available for the open/closed upvalue lifetime sub-problem only —
invoke it if the slot/heap hand-off on return, transitive capture or per-call
independence gets genuinely fiddly, not for routine emission. State in the plan
whether you intend to use it.

## Hand-off

Summarise: the upvalue resolution as built (local vs enclosing-upvalue,
transitive), the `Closure` object shape, the open→closed lifecycle and where the
close happens on return, how per-call independence is achieved, and the test files
added — and whether the `grob-closure-specialist` was invoked and for what. Note
for the next chat: Increment E is the top-level initialisation state machine
(§19.1, D-294 — the `SlotState` tag, `DefineGlobal`/`GetGlobal` transitions,
`_startupComplete`, **E5902**) and flow-sensitive narrowing (`if (x != nil)`
narrows `T?` to `T`). The closures built here are the mechanism that lets a
top-level lambda read a global during startup — E adds the tag that catches a read
before the global is initialised.
