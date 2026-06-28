---
description: "Sprint 6 · Increment C — field access and assignment. .field (GetProperty), .field = v (SetProperty), nested a.b.c, undefined-member E1002, readonly-field mutation E0204, AND the mandatory D-325 closure-in-field escape regression (a closure stored in a struct field that escapes its enclosing function closes its upvalue with no value-stack underflow)."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 6 · Increment C — field access and assignment

Increment B made types constructible. This increment makes their fields **readable and
writable**: `instance.field` reads through the `GetProperty` opcode, `instance.field =
value` writes through `SetProperty`, nested access `issue.user.login` resolves each step
in the type checker, undefined field access is **E1002**, mutating a field of a
`readonly` struct is **E0204** — and, the load-bearing cross-cutting test this sprint
inherits, a **closure stored in a struct field that escapes its enclosing function
closes its upvalue correctly** (D-325). It sits on Increment B's runtime struct value.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the Sprint 6 access bullets (field access,
   field assignment, nested access, undefined field access is a compile error),
   §3.1.1, §3.3 (confirm `GetProperty`/`SetProperty` are present), §3.5.
2. `docs/design/grob-language-fundamentals.md` — **§10** (field access and assignment
   semantics), the member-access grammar, and the nested-access resolution rule (the
   checker resolves each step of `a.b.c` against the type of the preceding step).
3. `docs/design/grob-vm-architecture.md` — the `GetProperty` / `SetProperty` handler
   sketches (1-byte name index; the struct value on the stack), **and the upvalue
   lifecycle section** (open vs closed upvalues, the open-upvalue list keyed by stack
   slot, the frame-exit close sweep — D-325). This last is what the closure-in-field
   escape exercises.
4. `docs/design/grob-error-codes.md` — confirm **E1002** (undefined member) and
   **E0204** (mutation of `readonly` value, including fields of a `readonly` struct).
5. Decisions: confirm **D-325** (upvalue closing is location-based, not value-based;
   route-agnostic across array element, map value, **struct field**, parameter and
   return — the struct-field route was "absent only because structs are Sprint 6"),
   **D-296** (the four-category capture model), and **D-204/whichever governs
   `readonly` deep immutability**. Grep the log.

> **Verify before relying on cited decisions and sections.** Grep §10 for the
> field-access and nested-resolution rules, `grob-vm-architecture.md` for the
> `GetProperty`/`SetProperty` and upvalue-lifecycle sections, and the decisions log for
> D-325 and D-296. If a section has moved or a decision been superseded, surface it.
>
> **Field-access rules — inline reference (authoritative source is §10 and
> `grob-vm-architecture.md`; reproduced here so the implementation does not depend on a
> fetch landing well).**
>
> - **Field access (`GetProperty`).** `instance.field` resolves the field against the
>   instance's type at compile time; an access to a field the type does not declare is
>   **E1002** "undefined member" with a source location. The compiler emits the
>   instance expression, then `GetProperty` with the field's name index; the VM reads
>   the field from the struct value on the stack.
> - **Field assignment (`SetProperty`).** `instance.field = value` resolves the field,
>   checks the value's type against the field's type (the existing assignment-mismatch
>   code — confirm it), then emits the instance, the value and `SetProperty` with the
>   name index; the VM writes the field. Mutating a field of a `readonly` binding is
>   **E0204** (deep immutability — a `readonly` struct's fields cannot be reassigned).
> - **Nested access (`a.b.c`).** The checker resolves each step against the type of the
>   preceding step: `a` has type `A`, `A.b` has type `B`, `B.c` has type `C`. Each step
>   is its own `GetProperty`. An undefined step is **E1002** at that step. Nested
>   assignment (`a.b.c = v`) reads `a.b` then `SetProperty` on the final field.
> - **No new opcodes.** `GetProperty` and `SetProperty` already exist in the closed
>   enum (Sprint 2 A). This increment writes their emission and dispatch arms. If you
>   reach to add an enum case, stop and surface.
>
> **The closure-in-field escape — mandatory regression (D-325), NOT optional.** A
> struct field can now hold a closure value. D-325 made upvalue closing **location-
> based and route-agnostic** precisely so that array element, map value, struct field,
> parameter and direct return all close identically — and its entry recorded the
> struct-field route as **absent only because structs were Sprint 6**. This increment is
> where that route first exists, so this increment must prove it:
>
> - A function that captures an enclosing-function local, stores the resulting closure
>   in a **struct field**, and **returns the struct** (so the closure escapes the frame
>   that created its captured slot), must close the upvalue into its heap cell on frame
>   exit — exactly as the array-escape case (`return [inc]` then `arr[0]()`) does.
> - Calling the closure **through the field** after the enclosing function has returned
>   (`s.inc()`, or `getStruct().inc()`) must read the **closed** upvalue and produce the
>   correct value, with **no value-stack underflow** — the exact failure D-325 fixed for
>   the container route.
> - This is **verification over the settled D-325 mechanism**, not new closure work. The
>   open-upvalue list is keyed by stack slot and the frame-exit sweep closes every open
>   upvalue at or above the returning frame's base **regardless of which heap object now
>   references the closure** — so the struct-field route should close correctly with no
>   new VM logic. If it does **not**, that is a D-325 regression and a finding — surface
>   it, do not patch around it. Concerns category-4 capture only (D-296); a struct field
>   holding a top-level `fn` (no upvalues, D-321) is a separate, trivially-correct case
>   worth a second test.
>
> **Sequencing note.** This is Increment C: A (declarations) → B (construction) →
> **C (access + assignment)** → D (anonymous structs) → E (close). Do not pull anonymous
> structs or the brace-disambiguation diagnostic forward.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the type-checker access/assignment
   resolution (E1002, the assignment-mismatch check, E0204), the nested-access
   resolution, the `GetProperty`/`SetProperty` emission and dispatch, the closure-in-
   field escape tests, and the rest of the tests — and wait for Chris's approval.
2. On approval, run `/start-branch` and propose `feat/field-access`. Wait for Chris.
3. Implement with TDD — tests confirmed **failing** first. Follow the `tdd-cycle`,
   `defining-a-type` and `adding-an-opcode` skills.
4. Run `/commit-message` against the staged changes.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–5** and **Sprint 6 Increments A–B**: the type registry (A), named
  construction with defaults and the runtime struct value via `NewStruct` (B). The
  struct value is the surface this increment reads and mutates.
- **D-325 location-based upvalue closing** (merged in the Sprint 5 interlude): the
  open-upvalue list keyed by stack slot and the route-agnostic frame-exit close sweep.
  This increment exercises the struct-field route it was built to handle.

## Deliverable for this increment

Field access and assignment across the type checker, the compiler and the VM, plus the
D-325 closure-in-field escape verification. **No anonymous structs.**

1. **Type checker — field access.** Resolve `instance.field` against the instance's
   type; **E1002** for an undefined field. Resolve nested `a.b.c` step by step.
   Annotate each access with the field's type. Set `ResolvedType` and `Declaration` on
   every identifier and member node; the §3.1.1 invariant holds.
2. **Type checker — field assignment.** Resolve the target field, check the assigned
   value's type, **E0204** for mutation through a `readonly` binding.
3. **Compiler — access and assignment.** Emit instance + `GetProperty` for a read;
   instance + value + `SetProperty` for a write; the nested chain as successive
   `GetProperty` steps ending in a final read or `SetProperty`.
4. **VM — `GetProperty` / `SetProperty` dispatch.** Read and write the named field on
   the struct value against `grob-vm-architecture.md`.
5. **Closure-in-field escape (D-325).** The mandatory regression: a closure stored in a
   struct field that escapes its enclosing function closes its upvalue and is callable
   through the field afterwards with no value-stack underflow. A struct field holding a
   no-upvalue top-level `fn` as the trivially-correct companion case.
6. **Diagnostics via `ErrorCatalog` (D-308).** E1002, E0204 and the assignment-mismatch
   code raise through catalog descriptors. No literals; no invented codes.

No new opcodes beyond wiring the closed-enum `GetProperty`/`SetProperty`, no parser or
AST edits, no anonymous structs.

## Out of scope

Anonymous structs, `NewAnonStruct`, structural typing and the brace-disambiguation
diagnostic (Increment D). The smoke script and benchmark baseline (Increment E). Do not
edit the parser, the AST or the `OpCode` enum.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - `instance.field` resolves and annotates the access with the field's type; an
    undefined field is **E1002** with a source location.
  - Nested `a.b.c` resolves each step; an undefined step is **E1002** at that step.
  - `instance.field = value` with a mismatched value type is the assignment-mismatch
    code; mutation through a `readonly` binding is **E0204**.
  - An access disassembles to instance + `GetProperty` (name-index operand verified
    through the disassembler); an assignment to instance + value + `SetProperty`.
  - §3.1.1 invariant holds for every identifier and member node introduced.
  - Layer-invariant `[Theory]` rows: pathological but parseable access/assignment inputs
    type-check to a result or a diagnostic, never throw.
- **VM tests (`Grob.Vm.Tests`):**
  - A hand-built chunk reads a field with `GetProperty` and writes one with
    `SetProperty`, leaving the stack at the expected depth.
  - **Closure-in-field escape (D-325):** a closure capturing an enclosing-function local,
    stored in a struct field and returned, is callable through the field after the
    enclosing function returns and reads the **closed** upvalue correctly, with **no
    value-stack underflow**. A second `makeCounter`-style construction yields an
    independent captured slot (per-call independence holds through the field route).
  - A struct field holding a no-upvalue top-level `fn` (D-321) is callable through the
    field and is trivially correct (the companion case).
- **Integration tests (`Grob.Integration.Tests`):**
  - A small script constructing a value, reading and assigning its fields, and reading a
    nested field, runs end-to-end via `grob run`.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- Field access reads via `GetProperty`; assignment writes via `SetProperty`; nested
  access resolves; undefined fields are E1002; readonly-field mutation is E0204.
- **The D-325 closure-in-field escape closes correctly with no value-stack underflow**
  and is callable through the field after the enclosing frame returns.
- The §3.1.1 invariant holds; the DAG holds; diagnostics raise through catalog
  descriptors; no literals, no invented codes; error-code count unchanged at 110.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. Access and assignment are settled by §10 and
`grob-vm-architecture.md`; the closure-in-field escape is verification over the settled
D-325 mechanism, not new closure design. No Opus carve-out — if the upvalue interaction
through the struct-field route turns out to need genuine new VM logic rather than a
test, **stop and surface it as a possible D-325 regression** rather than reaching for
Opus.

## Hand-off

Summarise: the field-access and assignment resolution and codes (E1002, E0204,
assignment-mismatch), the `GetProperty`/`SetProperty` arms as built, the nested-access
resolution, **the closure-in-field escape result** (closed correctly, or a surfaced
D-325 regression), and the test files added. Note for the next chat: Increment D is
anonymous structs — `#{ }` (`NewAnonStruct`), the internal structural type, structural
field access, anon structs in lambdas and `.select()` projections, nested `#{ }`, and
the `{ }` / `#{ }` / `TypeName { }` disambiguation (E2101). No `#{...}` annotation form.
