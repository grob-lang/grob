---
description: "Sprint 4 · Increment B — while and loop control. The backward Loop jump, the loop-context stack that break/continue resolve against, and the break/continue-outside-a-loop compile errors E2211/E2212. The structural increment: C, D and E build on this loop-context model."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 4 · Increment B — `while` and loop control

Increment A delivered the forward-jump conditionals. This increment adds the
first **loop** — the backward `Loop` jump for `while` — and the **loop-context
stack** that `break` and `continue` resolve against. This is the structural
increment of Sprint 4: `for...in` (C) lowers to `while`, and `select` (D) must
be aware of the loop context to *not* capture `break`/`continue`. An awkward
loop-context model here is expensive to unwind in C and D.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 4** scope (the `while`
   and `break`/`continue` bullets), §3.3 (`Loop` — the backward jump;
   `JumpIfFalse` — the loop-exit), §3.1.1, §3.5.
2. `docs/design/grob-language-fundamentals.md` — **§2** (`while`), **§4**
   (`break` and `continue`). Grep for the rule that `break`/`continue` outside a
   loop is a compile error, and (for the §4 forward-reference that matters in D)
   that `break`/`continue` inside a `select` apply to an enclosing loop, not the
   `select`.
3. `docs/design/grob-vm-architecture.md` — the backward-jump (`Loop`) offset
   mechanics and how it differs from a forward `Jump`.
4. `docs/design/grob-error-codes.md` — the **E22xx statement-context block**.
   E2207 already covers `return`/`break`/`continue` inside `finally`; this
   increment registers the *outside-a-loop* errors at the next free numbers.
5. Decisions: confirm any entry on loop compilation or jump mechanics. Grep the
   log.

> **Verify before relying on cited sections and codes.** Grep
> `grob-language-fundamentals.md` for §2 and §4, and `grob-error-codes.md` for
> the E22xx tail, before assigning. If the next-free E22xx numbers are not
> E2211/E2212 (because something landed since this prompt was written), use the
> real next-free pair and note the correction — do not invent or collide.
>
> **`while` and loop-control rules — inline reference (authoritative source is
> `grob-language-fundamentals.md`).**
>
> - **`while (cond) { }`.** The condition is `bool`. Compile: a loop-top label;
>   evaluate `cond`; `JumpIfFalse` to the loop-exit (forward, backpatched);
>   the body; a `Loop` backward jump to the loop-top. The exit target is
>   backpatched after the body. Block scoping and `PopN` for the body's locals
>   reuse Sprint 3 A.
> - **`break`.** Exits the **innermost** enclosing loop: an unconditional
>   forward `Jump` to the loop-exit, recorded on the loop context and backpatched
>   when the loop closes (a loop may have several `break`s, all patched to the
>   same exit). `break` outside any loop is a compile error.
> - **`continue`.** Skips to the next iteration of the innermost enclosing loop.
>   For `while`, that is a jump back to the loop-top (the condition). **Note for
>   C:** when `for...in` lowers to `while`, `continue` must target the
>   *increment step*, not the condition, or the counter never advances — so the
>   loop context must expose a "continue target" that the lowering can set, not a
>   hard-coded jump-to-condition. Shape the context for that now. `continue`
>   outside any loop is a compile error.
> - **The loop-context stack.** The compiler maintains a stack of loop contexts.
>   Entering a loop pushes one (carrying its exit-jump list and its continue
>   target); leaving pops it and backpatches the `break` jumps. `break`/`continue`
>   resolve to the top of the stack. A `select` (Increment D) is **not** a loop
>   and must **not** push a context — so a `break`/`continue` inside a `select`
>   case resolves to the enclosing loop if one exists, or is the outside-a-loop
>   error if not. Build the stack so D gets this for free.
> - **`break`/`continue` inside `finally`** is already E2207 (Sprint 7 surface,
>   parser-enforced earlier). Do not touch it; this increment is the
>   *outside-a-loop* case only.
>
> **Sequencing note.** This is Increment B: A (conditionals) → **B (`while` +
> `break`/`continue`)** → C (`for...in`) → D (`select`) → E (switch expression)
> → F (close). Do not pull `for...in` or `select` forward. The loop-context
> stack you build here is the thing C and D consume — get its shape right, then
> stop at `while`.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the bool-condition check,
   the `while` emission, the loop-context stack design (this is the piece to get
   right), the `break`/`continue` resolution and backpatching, the E2211/E2212
   registration, and the tests — and wait for approval before editing.
2. On approval, `/start-branch` → propose `feat/while-and-loop-control`. Wait
   for the branch.
3. TDD — tests confirmed **failing** first.
4. `/commit-message` against staged changes.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop and surface.

## What is already done

- **Sprints 1–3:** front end, back end, variables/scope, `grob run`,
  `const`/`readonly`, the nullable runtime and the `emitJump`/`patchJump`
  helper, interpolation, the REPL, the first VM benchmark baseline. All green.
- **Sprint 4 A:** `if`/`else if`/`else`, `&&`/`||` short-circuit and the
  ternary — forward-jump conditionals over the reused `patchJump` helper.

## Deliverable for this increment

`while` and loop control across the type checker and compiler.

1. **Type checker — `while` condition and loop-control placement.** Validate the
   `while` condition is `bool`; validate that every `break`/`continue` is inside
   a loop. A `break`/`continue` outside any loop is a compile error.
2. **Compiler — `while`.** Loop-top label, condition, `JumpIfFalse` exit
   (backpatched), body, `Loop` backward jump. Body block scoping and `PopN`
   reuse Sprint 3 A.
3. **Compiler — the loop-context stack.** Push on loop entry (exit-jump list +
   continue target), pop and backpatch on exit. Expose the continue target as
   settable, so C's lowering can point it at the increment step. `select` must
   not push a context.
4. **Compiler — `break` / `continue`.** Resolve to the innermost loop context;
   `break` records a forward exit jump, `continue` jumps to the context's
   continue target.
5. **Diagnostics — register E2211 / E2212** (or the real next-free E22xx pair):
   `break` outside a loop, `continue` outside a loop. Add to
   `grob-error-codes.md` (summary index row + full entry, ADR-0014 numbering)
   and the `ErrorCatalog` (D-308) — the `"Exxxx"` string appears exactly once,
   never a literal at the call site.

No new opcodes (the enum is closed). The `Loop` opcode already exists; this
increment wires its first use in the VM dispatch if Sprint 3 did not (confirm —
`?.` used forward jumps only, so `Loop` may be unwired). No parser or AST edits.

## Out of scope

`for...in` (Increment C) — do not lower anything to `while` here; `for` is C's
job. `select`/`case` (Increment D) — but build the loop-context stack so D's
pass-through works. The switch expression (Increment E). Flow narrowing (Sprint
5). Do not edit the parser, AST or `OpCode` enum.

## Tests

Per §3.5.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - A non-`bool` `while` condition is a compile error.
  - `break`/`continue` outside any loop are E2211/E2212 with source locations.
  - A `while` disassembles to loop-top + condition + `JumpIfFalse`-exit + body +
    `Loop`, with the exit and `Loop` offsets landing correctly.
  - A `while` with two `break`s patches both to the same exit; a `continue`
    targets the loop-top (the condition) for `while`.
  - A nested `while` resolves `break`/`continue` to the **inner** loop; the outer
    loop is untouched.
  - §3.1.1 invariant holds for every identifier node introduced.
- **VM tests (`Grob.Vm.Tests`):**
  - A counted `while` (e.g. `i := 0; while i < 3 { print(i); i = i + 1 }`)
    executes exactly the expected iterations and stops.
  - `break` exits early; `continue` skips the rest of the body and re-tests.
  - `Loop` backward-jumps to the correct instruction (no off-by-one that runs
    one iteration too many or few).
- **Integration tests (`Grob.Integration.Tests`):**
  - A script with a `while` loop, a `break` and a `continue` runs end-to-end and
    produces the expected stdout.

## Acceptance

- `dotnet build` and `dotnet test` green.
- `while` loops, `break` and `continue` work; nested loops resolve loop control
  to the innermost loop; `break`/`continue` outside a loop are E2211/E2212.
- The loop-context stack is built so a non-loop `select` will not capture
  `break`/`continue`, and so C's lowering can set the continue target.
- `Loop` and the exit `JumpIfFalse` offsets land correctly (verified by
  disassembly and by VM iteration counts).
- The §3.1.1 invariant holds; the DAG holds; no parser/AST/enum edits.
- E2211/E2212 registered in the registry and the catalog; no literals, no
  invented numbers.

## Model

Sonnet 4.6 (High) throughout. The `while` emission and the `break`/`continue`
resolution are settled by the inlined rules. The one structural candidate is the
**loop-context stack shape** — C's `for...in` lowering and D's `select`
pass-through both consume it, so an awkward shape costs later. With the rules
above in front of it Sonnet handles it; reach for the Opus subagent only if the
continue-target indirection (so C can retarget it to the increment step) and the
non-loop-`select` pass-through interact in a way that needs a judgement call.
The trigger is "C and D build on this shape", not "this part is hard".

## Hand-off

Summarise: the `while` emission, the **loop-context stack design** (what each
context carries, how `break` exit jumps are collected and patched, how the
continue target is exposed and settable, and how a non-loop construct will avoid
pushing a context), the `break`/`continue` resolution, the E2211/E2212
registration, and the test files added. Note for the next chat: Increment C is
`for...in` — all forms (array, index `(i, item)`, map `(k, v)`, numeric range
with `step`/descending) lowered to `while` over this loop-context model, with
the iteration diagnostics at **E0501** onward (the empty E05xx sub-block in the
Type category) and the Opus `grob-lowering-specialist` subagent available for
the lowering sub-problem.
