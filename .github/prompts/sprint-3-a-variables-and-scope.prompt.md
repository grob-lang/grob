---
name: "Sprint 3 · Increment A — Variables, Assignment and Scope"
description: "Build mutable declarations and assignment in Grob.Compiler and Grob.Vm — :=, = scope-chain walk, the globals table, locals with stack slots, block scoping with PopN, type annotations, ++/-- and compound assignment. The foundational layer every later Sprint 3 increment builds on."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 3 · Increment A — Variables, Assignment and Scope

You are opening Sprint 3 of the Grob compiler. Sprint 2 delivered the back end
— primitives, disassembler, VM dispatch loop, the two-pass type checker and the
compiler — for an expression-only surface (`print(2 + 3 * 4)` → `14`). This
increment makes the language declare and reassign: `:=`, `=` walking the scope
chain, globals, locals on stack slots, block scoping, type annotations and the
assignment sugar (`++`/`--`, `+=`). It is the foundation the rest of Sprint 3
stands on, so the slot and scope model built here is load-bearing.

Read, in order:

1. `docs/design/grob-v1-requirements.md` §4 (Sprint 3 scope — the
   declaration/assignment/scope/locals/globals/`PopN` bullets), §3.3 (the
   `OpCode` enum — `DefineGlobal`/`GetGlobal`/`SetGlobal`, `GetLocal`/`SetLocal`,
   `PopN`, `IncrementInt`/`DecrementInt`), §3.1.1 (the day-one LSP properties),
   §3.5 (test-project routing).
2. `docs/design/grob-language-fundamentals.md` — the declaration/assignment
   sections (`:=`, `=` and compound, `++`/`--`), the scope-chain rules and the
   four-category variable-resolution model. Grep for the assignment-target rules
   and the scope-lifetime rules.
3. `docs/design/grob-vm-architecture.md` — the value-stack and locals/slots
   sections, and how globals are represented.
4. `docs/design/grob-solution-architecture.md` — `Grob.Compiler` and `Grob.Vm`
   responsibilities and the DAG (`Grob.Compiler` references `Grob.Core`, never
   `Grob.Vm`; `Grob.Vm` references `Grob.Core` and `Grob.Runtime`, never
   `Grob.Compiler`).
5. Decisions **D-137** (`SourceLocation` day-one), **D-166** (two-pass type
   checker), **D-311** (`UnresolvedDecl.Instance` sentinel) in
   `docs/design/grob-decisions-log.md`.

> **Verify before relying on cited decisions.** Grep the decisions log to
> confirm D-137, D-166 and D-311 say what this prompt assumes (D-137 =
> `SourceLocation` on every AST node; D-166 = two-pass registration; D-311 =
> the `UnresolvedDecl.Instance` singleton on the declaration side, symmetric
> with `GrobType.Error`). If any has been superseded or renumbered, surface it
> rather than proceeding.
>
> **Declaration and assignment rules — inline reference (authoritative source
> is `grob-language-fundamentals.md`; reproduced here so the implementation does
> not depend on a fetch landing well).**
>
> - **`:=` declaration.** Declares a name in the current scope and assigns the
>   right-hand side. Valid only on first use of the name in the current scope.
>   Reusing `:=` for a name that already exists in the current scope is a
>   compile error — the user must use `=`.
> - **`=` reassignment.** The left-hand side must be an assignable target: a
>   local or global name, a struct field access (`obj.field`) or an array index
>   (`arr[i]`). Field and index targets are not in this increment's surface
>   (structs are Sprint 6, indexed assignment lands with collections) — accept
>   the name-target form and leave the others to their sprints. The name must
>   already exist (declared earlier with `:=`, or — later — a parameter). The
>   resolution walks the scope chain inner-to-outer. Assigning to an undeclared
>   name is a compile error.
> - **Compound assignment.** `x += y` lowers at compile time to `x = x + y`;
>   likewise `-=`, `*=`, `/=`, `%=`. The type rules of the underlying binary
>   operator apply (resolved by the type checker, which is already in place).
> - **`++`/`--`.** Postfix only — prefix is not valid. `int` only; `float++`
>   and `float--` are compile errors. `++`/`--` on a `const` binding is a
>   compile error. The compiler lowers `i++` to `i = i + 1`. `IncrementInt`/
>   `DecrementInt` are the dedicated opcodes; see the emission note below.
> - **No assignment in expression position.** `if (x = 5)` is a parse error,
>   not this increment's concern — the parser already enforces it.
>
> **Scope and slots — inline reference.**
>
> - A `{ }` block creates a new scope. Bindings live until the end of their
>   enclosing block. On block exit the compiler emits `PopN` to discard the
>   block's locals in one instruction.
> - Top-level bindings are globals: `DefineGlobal` creates the binding,
>   `GetGlobal`/`SetGlobal` read and write by name index.
> - Bindings inside a function or block are locals on the value stack:
>   `GetLocal`/`SetLocal` index a stack slot (1-byte slot index per §3.3). The
>   compiler tracks slot allocation as it walks scopes.
> - The four-category resolution model in the fundamentals is the reference for
>   how a name resolves; in this increment only the top-level-global and
>   enclosing-block-local categories are exercised (upvalues and the `const`-
>   inline category arrive in Sprint 5 and Increment C respectively).
>
> **Sequencing note.** This is Increment A of the agreed Sprint 3 breakdown:
> **A (variables, assignment, scope)** → B (`grob run`) → C (`const`/`readonly`)
> → D (nullable) → E (interpolation) → F (REPL + close). Do not pull `const`/
> `readonly` declarations, nullable handling or interpolation forward — those
> are their own increments. This increment is mutable bindings only.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name `feat/variables-and-scope`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprint 1:** complete front end — `TokenKind` + `Token`, lexer, the
  grammar-complete AST (including the declaration, assignment,
  compound-assignment and increment/decrement nodes — `CompoundAssignmentOperator`,
  `IncrementKind`, `TypeRef` already exist), the error-recovering parser and the
  recovery acceptance suite. All green.
- **Sprint 2:** `OpCode` (closed enum), `GrobValue` (D-303), `Chunk` in
  `Grob.Core`; `Disassembler` and the VM dispatch loop in `Grob.Vm`
  (`ValueStack`, arithmetic, `Constant`/`Print`/`Return`, checked arithmetic,
  `#if DEBUG` `TraceInstruction`); the two-pass type checker in `Grob.Compiler`
  (scalar `:=` inference, arithmetic/comparison type rules, `ResolvedType` and
  `Declaration` on every identifier node, `UnresolvedDecl.Instance` sentinel per
  D-311, collect-all-errors, `Error`-type cascade suppression); the compiler
  (typed-opcode emission from `ResolvedType`, constant pool, line tracking,
  `print()`/`exit()`); the BenchmarkDotNet skeleton and first compile baseline
  via the `benchmark.yml` workflow (D-309). C# 14 / .NET 10 (D-310),
  `ErrorCatalog` (D-308). QA and QA-fix landed.

## Deliverable for this increment

Mutable variable declaration, assignment and scope across the type checker
(`Grob.Compiler`) and the VM (`Grob.Vm`). The parser and AST are stable — every
node you need exists. **No `const`/`readonly`, no nullable handling, no
interpolation.**

1. **Type checker — declaration registration and assignment validation.** Extend
   the existing two-pass visitor: register `:=` bindings in the active scope with
   their inferred (or annotated) type; reject re-`:=` of a name in the same
   scope; resolve `=` targets by walking the scope chain and reject assignment
   to undeclared names; validate compound-assignment and `++`/`--` operands
   against the underlying operator's type rules. Set `ResolvedType` and
   `Declaration` on every identifier node introduced here — the §3.1.1 invariant
   holds for declaration and reference identifiers, with `UnresolvedDecl.Instance`
   at any unresolved path (D-311).
2. **Type annotations.** `name: Type := value` — validate the right-hand side
   against the annotation via the existing `TypeRef` node. A mismatch is a
   compile error with a source location.
3. **Compiler — globals.** Emit `DefineGlobal` for top-level bindings,
   `GetGlobal`/`SetGlobal` for reads and writes by name index. Manage the
   global name-index table.
4. **Compiler — locals and slots.** Allocate stack slots for block-scoped
   bindings; emit `GetLocal`/`SetLocal` by slot index. Track slot allocation as
   the compiler walks nested scopes.
5. **Compiler — block scoping and `PopN`.** A `{ }` block opens a scope; on exit
   emit a single `PopN` discarding exactly the block's locals.
6. **Compiler — assignment sugar.** Desugar compound assignment (`x += y` →
   `x = x + y`) and `++`/`--` (`i++` → `i = i + 1`). Emit `IncrementInt`/
   `DecrementInt` for the `int` increment/decrement path (the dedicated opcode is
   the peephole over the load-add-store lowering); the load-operate-store path
   covers compound assignment. **Wire `IncrementInt`/`DecrementInt` only** —
   `float++` is a compile error per the fundamentals, so `IncrementFloat`/
   `DecrementFloat` stay unwired. If you believe `float++` should be legal, stop
   and surface; do not wire the float arms.
7. **VM — opcode arms.** Implement `DefineGlobal`/`GetGlobal`/`SetGlobal`,
   `GetLocal`/`SetLocal`, `PopN`, `IncrementInt`/`DecrementInt` in the dispatch
   loop. A globals table (name → `GrobValue`) lives in the VM's runtime state.
   Reading an undefined global at runtime is a runtime error carrying the source
   line (the same first-error-stops discipline as Sprint 2's arithmetic).
8. **Diagnostics via `ErrorCatalog` (D-308).** `++`/`--` on a `const` reuses
   **E0201** (reassignment of const). Reassigning an undeclared name and
   re-`:=` in scope: confirm the registered name-resolution code in
   `grob-error-codes.md` (E1001 is undefined-identifier) and reuse it, or — if a
   distinct code is needed and none exists — **stop and surface for a code
   assignment; do not invent a number.** `float++` is a compile error per the
   fundamentals; confirm its registered code and reuse it, or stop and surface
   if none exists. Every new or reused code is referenced via a catalog
   descriptor, never a literal at the call site.

## Out of scope

No `const`/`readonly` (Increment C) — mutable `:=` only. No nullable types,
`??`, `?.`, `IsNil`, `NilCoalesce` (Increment D). No string-interpolation
compilation (Increment E). No `grob run` CLI (Increment B). No control flow,
jumps or `Loop` (Sprint 4). No functions, call frames, upvalues or
`GetUpvalue`/`SetUpvalue` (Sprint 5). No struct field or array-index assignment
targets (Sprint 6 / collections). Do not edit the parser or AST — they are
grammar-complete from Sprint 1. Do not extend the `OpCode` enum — it is closed
(Sprint 2 A); if you believe you need an opcode that is not there, stop and
surface.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - `x := 5` registers a global; `x = 6` resolves and reassigns; `x = 6`
    without a prior `x := …` is a compile error.
  - Re-`:=` of a name in the same scope is a compile error.
  - A `{ }` block declares a local that is not visible after the block; the
    compiler emits `PopN` discarding exactly the block's locals.
  - `name: int := "x"` (annotation mismatch) is a compile error with a source
    location.
  - `x += 1` emits the load-operate-store sequence; `i++` emits the increment
    path; `++` on a `const` is a compile error (E0201).
  - The emitted bytecode for a representative declaration/assignment script
    disassembles (Sprint 2 A) to a correct, readable listing — verify through
    the disassembler, not only the VM's answer.
  - §3.1.1 invariant: every identifier node in a type-checked
    declaration/assignment tree carries a non-null `ResolvedType` and a non-null
    `Declaration` (the sentinel `UnresolvedDecl.Instance` at error paths,
    asserted by reference with `Assert.Same`).
- **VM tests (`Grob.Vm.Tests`):**
  - Hand-construct (or compile, via the integration harness) a chunk that
    defines a global, reads it back, reassigns it and reads again — assert the
    values.
  - A local round-trips through `GetLocal`/`SetLocal` at the expected slot.
  - `PopN` discards the right count.
  - `IncrementInt` increments a slot; reading an undefined global is a runtime
    error with the correct source line.
- **Integration tests (`Grob.Integration.Tests`):**
  - A small script — declare, reassign, `print` the result — runs end-to-end and
    produces the expected stdout. (No CLI yet; use the integration harness as
    Sprint 2 D did.)

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- Declaration, reassignment and the scope chain work; re-`:=` in scope and
  assignment to an undeclared name are compile errors.
- Globals and slot-indexed locals round-trip through the VM; `PopN` cleans up
  block scopes.
- Compound assignment and `++`/`--` desugar and emit correctly; `float++` and
  `++` on a `const` are compile errors.
- The §3.1.1 invariant holds for every identifier node this increment
  introduces.
- The DAG holds; the `OpCode` enum and the parser/AST are unchanged.
- Diagnostics raise through `ErrorCatalog` descriptors; no code literals at call
  sites and no invented code numbers.

## Model

Sonnet 4.6 (High) throughout — the type-checker registration arms, the global
and slot emission, the `PopN` and desugaring are settled by the rules inlined
above and are transcription, not judgement. The one structural candidate is the
**local-slot allocation and block-scope tracking model** in the compiler: Sprint
4's control-flow blocks and Sprint 5's call frames and upvalues build directly
on how slots are allocated and how nested scopes push and pop. An awkward shape
here is expensive to unwind. With the clox local-slot model and the scope rules
inlined above in front of it, Sonnet handles it; reach for Opus only if the slot
lifetime across nested `{ }` blocks and the `PopN` count interact in a way that
needs a judgement call. The trigger is "later sprints build on this shape", not
"this part is hard".

## Hand-off

Summarise: the scope/slot model as built (how globals, block locals and slot
indices are tracked, and how `PopN` counts are computed), the assignment-target
resolution, the desugaring of compound assignment and `++`/`--`, the VM opcode
arms added, how the §3.1.1 invariant is verified, the diagnostics raised and
their catalog codes (including any new/reused codes for re-`:=`, assign-to-
undeclared and `float++`), and the test files added. Note for the next chat:
Increment B is `grob run` — a thin `RunCommand` in `Grob.Cli` that compiles and
executes a `.grob` file through the pipeline this increment can now produce, with
its first end-to-end script test in `Grob.Integration.Tests`.
