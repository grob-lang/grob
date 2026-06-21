---
description: "Sprint 5 · Increment E — initialisation state machine and narrowing. The top-level init state machine (§19.1, D-294): the SlotState tag, DefineGlobal/GetGlobal transitions, the _startupComplete short-circuit and the E5902 circular-init diagnostic; plus flow-sensitive narrowing — inside if (x != nil) the checker narrows x from T? to T. Two type-system/runtime features off the call-frame path."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 5 · Increment E — initialisation state machine and narrowing

Increment D finished the call/closure machinery. This increment delivers the two
type-system/runtime features that round off the sprint and sit off the call-frame
critical path: the **top-level initialisation state machine** (§19.1, D-294),
which catches a read of a top-level binding before its initialiser has run and
raises **E5902** on circular initialisation, and **flow-sensitive narrowing**,
which narrows a nullable `x` from `T?` to `T` inside an `if (x != nil)` block. They
are bundled because each is small and neither is call machinery — they share an
increment, not a mechanism.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 5** scope (the top-level
   initialisation state-machine bullet and the flow-sensitive-narrowing bullet),
   §6 Type System (the narrowing summary), §3.5 (test routing).
2. `docs/design/grob-language-fundamentals.md` — **§19.1** (the top-level
   initialisation state machine — the three-state tag, the transitions, the
   startup-only check, the circular-initialisation diagnostic) and the narrowing
   rule (`if (x != nil)` adds `x` to a known-non-nil set, narrowing `T?` to `T`,
   removed after the block). Grep for §19.1 and the narrowing rule.
3. `docs/design/grob-vm-architecture.md` — **Optional Type Narrowing
   (Flow-Sensitive Typing)** (the known-non-nil set on `if x != nil` blocks) and
   the globals table / `GetGlobal`/`DefineGlobal` mechanics the state machine tags.
4. `docs/design/grob-error-codes.md` — confirm **E5902** (circular initialisation,
   `RuntimeError`) already exists; reuse it, do not register a new code.
5. Decisions: **D-294** (top-level initialisation state machine). Grep the log; do
   not take its content from this prompt.

> **Verify before relying on cited decisions and sections.** Grep
> `grob-language-fundamentals.md` §19.1 and the narrowing rule, and
> `grob-error-codes.md` for E5902. If §19.1 or the narrowing rule reads
> differently than this prompt assumes, surface it rather than proceeding.
>
> **Init-machine and narrowing rules — inline reference (authoritative source is
> `grob-language-fundamentals.md` §19.1 and the narrowing rule, and D-294;
> reproduced here so the implementation does not depend on a fetch landing well).**
>
> - **Top-level initialisation state machine (§19.1, D-294).** Each top-level
>   binding slot carries a `SlotState` tag: `Uninitialised`, `Initialising`,
>   `Initialised`. `DefineGlobal` flips the tag from `Uninitialised` to
>   `Initialising` **before** the right-hand side evaluates, and to `Initialised`
>   once the value is stored. `GetGlobal` **during startup** consults the tag; a
>   read from a slot not yet `Initialised` raises a `RuntimeError`. A read from a
>   slot that is `Initialising` (a binding's initialiser depending, directly or
>   through a called function/lambda, on the binding itself) is the
>   **circular-initialisation** case — **E5902**.
> - **The `_startupComplete` short-circuit.** After the top-level code's final
>   instruction, the VM sets `_startupComplete`. Subsequent `GetGlobal` reads skip
>   the tag check — the cost is a startup-only branch. The tag machinery must not
>   penalise steady-state global reads.
> - **The rule applies to `readonly` and mutable top-level bindings alike.** Both
>   are tagged; both are subject to the startup check. (`const` is inlined and has
>   no runtime slot — D-296 category 1 — so it is not tagged.)
> - **Flow-sensitive narrowing.** Inside an `if (x != nil) { ... }` block, the type
>   checker narrows `x` from `T?` to `T` for the extent of the block, then removes
>   the narrowing after the block. A narrowed `x` may be dereferenced or used where
>   `T` is required without `?.`/`??`; outside the block `x` is `T?` again and an
>   unguarded dereference is **E0101** (nil dereference) as before. Match the
>   spec's exact narrowing trigger (`!= nil` guard form) and scope (block extent,
>   removed after). This increment narrows on the `if (x != nil)` form per §6 /
>   the fundamentals; it does not introduce narrowing on other forms unless the
>   spec specifies them — if the spec lists more guard forms, implement what it
>   lists and no more.
> - **No new opcodes, no new codes.** The state machine tags existing
>   `DefineGlobal`/`GetGlobal` behaviour; E5902 already exists. Narrowing is a
>   type-checker-only feature — it changes which programs type-check, not the
>   opcode set. If either seems to need a new opcode or code, stop and surface.
>
> **Sequencing note.** This is Increment E: A → B → C → D → **E (init machine +
> narrowing)** → F (close). After E, only the close (F) remains.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the `SlotState` tag and the
   `DefineGlobal`/`GetGlobal` transitions plus `_startupComplete`, the E5902
   circular-init path, the narrowing pass in the type checker (the known-non-nil
   set on `if (x != nil)` blocks, removed after), and the tests — and wait for
   Chris's approval.
2. On approval, run `/start-branch` and propose `feat/init-and-narrowing`. Wait for
   Chris to create the branch.
3. Implement with TDD — tests written and confirmed **failing** first (follow
   `tdd-cycle`). The E5902 path follows `writing-an-error-test` for its gold-master
   pair (the code already exists — register no new code, but add the example pair
   if one is missing).
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–4** and **Sprint 5 A–D**: the call frame, recursion, named/default
  calls, lambdas, natives, the array higher-order methods, and closures with
  category-4 upvalue capture (open/closed upvalues, independent per-call capture).
  The closure mechanism is the thing that lets a top-level lambda read a global
  during startup — this increment adds the tag that catches a read before the
  global is `Initialised`. All green.

## Deliverable for this increment

The top-level initialisation state machine and flow-sensitive narrowing across the
VM and the type checker. **No call-machinery changes.**

1. **VM — the `SlotState` tag.** Add the three-state tag per top-level binding slot;
   `DefineGlobal` transitions `Uninitialised → Initialising → Initialised`;
   `GetGlobal` during startup consults the tag and raises a `RuntimeError` on a
   non-`Initialised` read, with the `Initialising` case being **E5902** circular
   initialisation. Set `_startupComplete` after top-level code; subsequent reads
   skip the check.
2. **Type checker — narrowing.** Inside an `if (x != nil)` block, add `x` to a
   known-non-nil set so `x` is `T` rather than `T?` for the block's extent; remove
   it after. A narrowed dereference type-checks; an unguarded dereference outside
   the block remains **E0101**.
3. **Diagnostics via `ErrorCatalog` (D-308).** E5902 raises through its existing
   descriptor; E0101 behaviour is unchanged outside narrowed scopes. No new codes.

No new opcodes, no parser or AST edits.

## Out of scope

Anything in the call/closure machinery (A–D) — done. The close-gate script and the
benchmark baseline (Increment F). Narrowing forms the spec does not specify (do not
invent narrowing on `==`/switch/assignment unless §6 or the fundamentals lists
them). Do not edit the parser, the AST or the `OpCode` enum. If either feature
seems to need a new opcode or error code, stop and surface.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - Inside `if (x != nil) { }`, `x` has `ResolvedType` `T` (not `T?`); after the
    block, `x` is `T?` again.
  - A dereference of a nullable inside the narrowed block type-checks; the same
    dereference outside the block is **E0101**.
  - §3.1.1 invariant holds for every identifier node introduced; the narrowed and
    un-narrowed nodes both carry non-null `ResolvedType`/`Declaration`.
- **VM tests (`Grob.Vm.Tests`):**
  - A `GetGlobal` on a slot tagged `Uninitialised` during startup raises a
    `RuntimeError`; a slot read after its `DefineGlobal` completes succeeds.
  - A circular initialisation (a top-level binding whose initialiser reads itself,
    directly or via a called function/lambda) raises **E5902**.
  - After `_startupComplete`, a steady-state global read does not consult the tag
    (verify via behaviour: a read works without the startup branch firing).
- **Error-example pair (negative-test gate):** an E5902 `*_grob.txt` /
  `*_expected.txt` pair if one is not already present, generated from the
  implementation.
- **Integration tests (`Grob.Integration.Tests`):**
  - A script narrowing a nullable inside `if (x != nil)` and printing the narrowed
    value runs end-to-end; a script with a circular top-level initialisation fails
    with the E5902 diagnostic and exit code 1.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- The top-level initialisation state machine tags slots and catches non-initialised
  reads during startup; circular initialisation is **E5902**; `_startupComplete`
  short-circuits steady-state reads.
- Flow-sensitive narrowing narrows `T?` to `T` inside `if (x != nil)` and removes
  it after; E0101 holds outside narrowed scopes.
- The §3.1.1 invariant holds; no new opcodes; no new codes; the DAG holds.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. The state machine is specified in §19.1 and narrowing
is a bounded type-checker pass — settled mechanics, not judgement. No Opus
carve-out.

## Hand-off

Summarise: the `SlotState` transitions and the startup-only check as built, the
E5902 circular-init path, the narrowing pass (the known-non-nil set, its block
scope and removal), and the test files added. Note for the next chat: Increment F
is the sprint close — the `functions.grob` smoke script (recursion, named/default
args, a `makeCounter` closure, a `filter`/`select`/`sort` pipeline with a capturing
lambda, an `if (x != nil)` narrowing — Sprint 1–5 surface only, no stdlib modules),
the §6 acceptance, and the third VM-execution benchmark baseline against the
two-axis gate (D-313).
