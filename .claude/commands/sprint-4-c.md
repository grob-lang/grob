---
description: "Sprint 4 · Increment C — for...in. All forms (array, index (i,item), map (k,v), numeric range with step/descending) lowered to while over the Increment B loop-context model, plus the iteration diagnostics E0501–E0504. The heaviest feature increment; the Opus grob-lowering-specialist subagent is available for the lowering sub-problem."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 4 · Increment C — `for...in`

Increment B delivered `while` and the loop-context stack. This increment adds
`for...in` in all its forms — array, index `(i, item)`, map `(k, v)` and numeric
range with `step` and descending — each **lowered to `while`** by the compiler
over B's loop-context model. It is the heaviest feature increment in Sprint 4:
several lowering shapes plus a cluster of compile-time diagnostics. There is no
`for` opcode; everything is a lowering.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 4** `for...in` bullets
   (array single, index form, map `(k, v)`, single-ident-on-map error, ranges,
   `step`, descending, non-iterable error), §3.3 (the comparison and increment
   opcodes the lowering emits — `LessInt`/`LessEqualInt`/`GreaterEqualInt`,
   `GetIndex`, `AddInt`), §3.1.1, §3.5.
2. `docs/design/grob-language-fundamentals.md` — **§5** (`for...in`, including
   the numeric-range subsection: inclusive bounds, `step`, descending). Grep for
   the iterator-immutability rule, the two-identifier map requirement and the
   descending-without-negative-step rule.
3. `docs/design/grob-vm-architecture.md` — array length, `GetIndex` and the map
   keys representation (the insertion-order key set the map iteration walks).
4. `docs/design/grob-type-registry.md` — the `array` and `map<K, V>` method
   surface the lowering reads (length, `keys`, index).
5. `docs/design/grob-error-codes.md` — the **E05xx sub-block is empty** within
   the E0xxx Type category; this increment registers the iteration diagnostics
   there.

> **Verify before relying on cited sections and codes.** Grep §5 of the
> fundamentals and the E05xx tail of the registry before assigning. If E0501
> onward is not free (something landed since), use the real next-free numbers
> and note the correction.
>
> **`for...in` lowering rules — inline reference (authoritative source is §5;
> reproduced here so the implementation does not depend on a fetch).**
>
> - **Every form lowers to `while`** over B's loop-context stack. The
>   `continue` target must be set to the **increment step**, not the condition,
>   via the settable continue target B exposed — or the counter never advances.
> - **Array, single** — `for item in arr { }`: synthetic `i := 0`, while
>   `i < arr.length`, `item := arr[i]` (fresh, immutable in the body), body,
>   `i = i + 1`.
> - **Array, index** — `for i, item in arr { }`: as above with `i` the visible
>   zero-based `int` counter.
> - **Map** — `for k, v in m { }`: two-identifier form **required**. Materialise
>   the insertion-order keys **once** before the loop; iterate the keys array;
>   `v := m[k]` each iteration. Single-identifier `for k in m` is a compile error
>   suggesting `for k in m.keys`.
> - **Range** — `for i in lo..hi { }`: inclusive both bounds. Ascending: `i := lo`,
>   while `i <= hi`, body, `i = i + 1`.
> - **Range with `step` / descending** — `lo..hi step n`. Ascending step uses
>   `<=` and `i += n`; descending (negative step) uses `>=` and `i += n`. A
>   descending range (`hi < lo`) **without** an explicit negative `step` is a
>   compile error.
> - **The iterator variable is immutable within the body.** Reassigning `item`,
>   `i`, `k` or `v` inside the loop body is a compile error.
> - **Any other subject type** in `for...in` position (not array, map or numeric
>   range) is a compile error.
>
> **Diagnostics to register (E05xx, Type category — confirm next-free):**
>
> - **E0501** — `for...in` subject is not iterable (not array / map / range).
> - **E0502** — single-identifier `for...in` over a `map` (message suggests
>   `for k in m.keys`).
> - **E0503** — descending numeric range without an explicit negative `step`.
> - **E0504** — reassignment of a `for...in` iterator variable inside the body.
>
> **Sequencing note.** This is Increment C: A → B → **C (`for...in`)** → D
> (`select`) → E (switch expression) → F (close). `for...in` depends on B's
> `while` and loop-context model — it is built on them, not alongside. Do not
> pull `select` or the switch expression forward.

## The Opus subagent — when to use it

The lowering of the five forms to one `while` machine, with correct slot
lifetime for the synthetic bindings, the keys-array materialisation point, the
step-sign/comparison-opcode agreement and the `continue`-to-increment-step
target, is the one genuinely structural piece. Sonnet 4.6 (High) handles the
routine emission. **Delegate to the `grob-lowering-specialist` subagent (Opus
4.8) only if** those interactions get fiddly — a slot-lifetime or
`continue`-target judgement call across the forms. The trigger is "this specific
lowering interaction is fiddly", never "for-loops are hard". The subagent
returns a lowering design and edits the `for...in` lowering path only; the type
checker rules, the diagnostics and the tests are this session's work.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the type-checker iteration
   rules and the four diagnostics, the lowering for each form (call out which,
   if any, you expect to delegate to the subagent), the E0501–E0504
   registration, and the tests — and wait for approval before editing.
2. On approval, `/start-branch` → propose `feat/for-in`. Wait for the branch.
3. TDD — tests confirmed **failing** first.
4. `/commit-message` against staged changes.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop and surface.

## What is already done

- **Sprints 1–3 and Sprint 4 A–B:** the full pipeline through `while`, the
  loop-context stack with a settable continue target, the forward-jump
  conditionals, the nullable runtime and the `emitJump`/`patchJump` helper. All
  green.

## Deliverable for this increment

`for...in` in all forms across the type checker and the compiler.

1. **Type checker — iteration rules.** Validate the subject type (array, map or
   numeric range; else E0501); enforce the two-identifier map requirement
   (single-ident on a map is E0502); enforce descending-needs-negative-step
   (E0503); enforce iterator-variable immutability in the body (E0504); infer
   the iteration-variable types (`item` from element type, `i` as `int`, `k`/`v`
   from `map<K, V>`). The §3.1.1 invariant holds for the synthetic and visible
   iteration identifier nodes.
2. **Compiler — lowering.** Each form to `while` over B's loop-context model,
   `continue` targeting the increment step. Synthetic-binding slots allocated
   and `PopN`-discarded with Sprint 3 A's discipline; the map keys array
   materialised once before the loop. (Delegate the lowering sub-problem to the
   Opus subagent if it gets fiddly.)
3. **Diagnostics — register E0501–E0504** (or the real next-free E05xx run) in
   `grob-error-codes.md` (index rows + full entries, ADR-0014) and the
   `ErrorCatalog`. Each `"Exxxx"` appears exactly once; never a literal at a
   call site; never invented.

No new opcodes, no parser/AST edits.

## Out of scope

`select`/`case` (Increment D), the switch expression (Increment E), the
calculator close (Increment F). Struct or collection *semantics* beyond the
array/map iteration surface §5 names. Flow narrowing (Sprint 5). Do not edit the
parser, AST or `OpCode` enum.

## Tests

Per §3.5.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - Each form type-checks and infers the iteration-variable types correctly.
  - E0501 (non-iterable subject), E0502 (single-ident map → suggests `.keys`),
    E0503 (descending without negative step), E0504 (iterator reassignment) each
    fire with a source location and the right code.
  - Each form disassembles to its `while` lowering — confirm the comparison
    opcode matches the step direction, the keys array is materialised once, and
    `continue` targets the increment step (compile a `for` with a `continue` and
    read the disassembly).
  - §3.1.1 invariant holds for synthetic and visible iteration identifier nodes.
- **VM tests (`Grob.Vm.Tests`):**
  - Array single and index forms iterate every element in order.
  - Map `(k, v)` iterates insertion order; `v` matches `m[k]` each step.
  - `0..3` yields 0,1,2,3 (inclusive); `0..10 step 5` yields 0,5,10;
    `3..0 step -1` yields 3,2,1,0.
  - A `continue` inside a `for` advances the counter (does not hang); a `break`
    exits the `for`.
- **Integration tests (`Grob.Integration.Tests`):**
  - A script iterating an array, a map and a stepped range, with a `break` and a
    `continue`, runs end-to-end and produces the expected stdout.

## Acceptance

- `dotnet build` and `dotnet test` green.
- All `for...in` forms iterate correctly; `continue` advances the counter,
  `break` exits; nested `for`/`while` resolve loop control to the innermost loop.
- E0501–E0504 fire correctly and are registered in the registry and the catalog;
  no literals, no invented numbers.
- The §3.1.1 invariant holds; the DAG holds; no parser/AST/enum edits.

## Model

Sonnet 4.6 (High) for the type-checker rules, the diagnostics and the routine
emission. The **Opus `grob-lowering-specialist` subagent** is available for the
lowering sub-problem only, gated behind "this specific lowering interaction is
fiddly". Do not reach for it for the whole increment.

## Hand-off

Summarise: the lowering shape for each of the five forms (synthetic bindings,
their slots and `PopN` counts, the comparison/increment opcodes, the keys-array
materialisation point, the `continue` target), whether the Opus subagent was
used and for what, the four diagnostics and their codes, and the test files
added. Note for the next chat: Increment D is `select`/`case` — first-match, no
fall-through, `default` optional, non-exhaustive (D-301), compiled as an
equality + `JumpIfFalse` ladder, and crucially **not** pushing a loop context so
`break`/`continue` inside a case fall through to an enclosing loop (the B model
gives this for free).
