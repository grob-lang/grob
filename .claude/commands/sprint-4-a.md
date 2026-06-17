---
description: "Sprint 4 · Increment A — Conditionals. if / else if / else, && / || short-circuit and the ternary ?:, all jump-based over the emitJump/patchJump helper Sprint 3 D built. Forward jumps only — no loops, no loop control. The conditional-jump foundation B–E reuse."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 4 · Increment A — Conditionals

You are opening Sprint 4 of the Grob compiler — control flow. Sprint 3 turned
the expression-only back end into one that declares, scopes, reassigns and runs
script files, and Increment D of that sprint built the reusable
`emitJump`/`patchJump` forward-jump backpatching helper to compile `?.`
short-circuit. This increment is the first statement-level use of that helper:
`if`/`else if`/`else`, the short-circuiting `&&`/`||`, and the ternary `?:` —
all forward jumps, no loop, no loop control. It is the conditional-jump
foundation every later Sprint 4 increment reuses, so the jump-emission shape
built here is load-bearing.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 4 — Control Flow** scope
   (the `if`/`else`, `&&`/`||`, ternary bullets), §3.3 (the `OpCode` enum —
   `Jump`, `JumpIfFalse`, `JumpIfTrue`; note `&&`/`||` are jump-based, **not**
   dedicated opcodes), §3.1.1 (the day-one LSP properties), §3.5 (test routing).
2. `docs/design/grob-language-fundamentals.md` — **§1** (`if`/`else`), **§6**
   (Operators — the Logical subsection for `&&`/`||`), **§7** (Operator
   Precedence — ternary) and **§12** (Expressions vs Statements — the ternary is
   an expression, `if` is a statement). Grep for the bool-condition rule and the
   ternary-arm unification rule.
3. `docs/design/grob-vm-architecture.md` — the jump-offset mechanics (2-byte
   forward offset) and how the dispatch loop advances the instruction pointer
   over a jump.
4. Decisions: confirm the entry governing the **switch-expression / ternary**
   grammar and any entry on jump compilation. Grep the log; do not take their
   content from this prompt.

> **Verify before relying on cited decisions and sections.** Grep
> `grob-language-fundamentals.md` to confirm §1, §6, §7 and §12 say what this
> prompt assumes, and the decisions log for any entry on jump-based conditional
> compilation. If a section has moved or a decision been superseded, surface it
> rather than proceeding.
>
> **Conditional compilation rules — inline reference (authoritative source is
> `grob-language-fundamentals.md`; reproduced here so the implementation does
> not depend on a fetch landing well).**
>
> - **`if` / `else if` / `else`.** The condition must be `bool` — a non-`bool`
>   condition is a compile error. Compile: evaluate the condition, `JumpIfFalse`
>   over the then-block; an `else`/`else if` is reached by an unconditional
>   `Jump` placed at the end of the then-block, jumping past the else-block. An
>   `else if` chain is nested `if`s — each link is its own condition +
>   `JumpIfFalse`, and the chain's exit jumps are all backpatched to the same
>   end-of-statement target. Every jump offset is backpatched via the
>   `patchJump` helper from Sprint 3 D — **reuse it, do not reinvent it.**
> - **`&&`.** Short-circuits using `JumpIfFalse` — **not** a dedicated opcode.
>   `a && b` compiles to: evaluate `a`; `JumpIfFalse` to a label that leaves
>   `false` (or leaves `a`'s already-`false` value) on the stack as the result;
>   otherwise evaluate `b` and that is the result. Confirm the exact
>   stack-discipline the operator table specifies (whether the short-circuit
>   leaves the left operand or pushes a literal `false`) and match it. Both
>   operands are `bool`; a non-`bool` operand is a compile error.
> - **`||`.** Short-circuits using `JumpIfTrue` — the mirror of `&&`. `a || b`
>   evaluates `a`; `JumpIfTrue` past `b` with the truthy result; otherwise `b`.
>   Both operands `bool`.
> - **Ternary `cond ? a : b`.** An **expression**, not a statement. The
>   condition is `bool`; the two arms must unify to a single type (the
>   ResolvedType of the whole expression), the same arm-unification rule the
>   switch expression will use. Compiles jump-based: evaluate `cond`,
>   `JumpIfFalse` to the `b` arm, evaluate `a`, `Jump` past `b`; the `b` arm is
>   the false target. Exactly one arm executes.
> - **No statement uses `Loop`.** This increment emits only forward jumps
>   (`Jump`, `JumpIfFalse`, `JumpIfTrue`). The backward `Loop` jump is Increment
>   B's. If you reach for `Loop` here, stop — you are pulling `while` forward.
>
> **The `emitJump`/`patchJump` reuse is the point of this increment.** Sprint 3
> D built the helper as a reusable compiler primitive for `?.`. Confirm it is
> reusable (not inlined into the `?.` path); if it turns out to be a one-off,
> that is a finding to surface before building on it — Sprint 4 needs it to be a
> primitive. `if`, `&&`, `||` and `?:` all go through it.
>
> **Sequencing note.** This is Increment A of the agreed Sprint 4 breakdown:
> **A (conditionals)** → B (`while` + `break`/`continue`) → C (`for...in`) →
> D (`select`/`case`) → E (switch expression) → F (calculator close). Do not
> pull `while`, loops, `break`/`continue`, `for...in`, `select` or the switch
> expression forward — those are their own increments. This increment is the
> forward-jump conditionals only.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered implementation plan — the
   type-checker arms (bool-condition and ternary-arm rules), the compiler
   emission for `if`/`&&`/`||`/`?:`, the VM (none new — the jump opcodes already
   dispatch from Sprint 3 D) and the tests — and wait for Chris's approval
   before editing. The plan is the gate.
2. On approval, run `/start-branch` and propose `feat/conditionals`. Wait for
   Chris to create the branch.
3. Implement with TDD — tests written and confirmed **failing** first.
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–2:** the front end (lexer, grammar-complete AST including the
  `if`/`else`, logical-operator and ternary nodes, the error-recovering parser),
  and the back end (closed `OpCode` enum, `GrobValue` (D-303), `Chunk`,
  disassembler, VM dispatch loop, two-pass type checker, compiler, BenchmarkDotNet
  skeleton). C# 14 / .NET 10 (D-310), `ErrorCatalog` (D-308). All green.
- **Sprint 3:** variables, assignment and the scope chain (`:=`, `=`, globals,
  locals on slots, `PopN`, `++`/`--`, compound assignment); `grob run`;
  `const`/`readonly`; the nullable runtime (`T?`, `??`, `?.`, `IsNil`,
  `NilCoalesce`) **including the reusable `emitJump`/`patchJump` backpatching
  helper**; string interpolation; the REPL; the first VM-execution benchmark
  baseline. `grob run hello.grob` closes Sprint 3. All green, QA and QA-fix
  landed.

## Deliverable for this increment

Forward-jump conditional statements and expressions across the type checker and
the compiler. The parser and AST are stable — every node exists. **No loops, no
`break`/`continue`, no `for`, no `select`, no switch expression.**

1. **Type checker — bool conditions.** Validate that `if`/`else if` conditions
   and `&&`/`||` operands and the ternary condition are `bool`. A non-`bool`
   is a compile error with a source location (reuse **E0001** type mismatch /
   **E0002** incompatible operands as the registry dictates — confirm which, do
   not invent). Set `ResolvedType` and `Declaration` on every identifier node
   introduced; the §3.1.1 invariant holds.
2. **Type checker — ternary arm unification.** The two arms unify to one type,
   which is the `ResolvedType` of the ternary expression. Arms that do not unify
   are a compile error (confirm the code; reuse E0001 or the registry's
   unification code). This is the same unification the switch expression (E)
   will reuse — shape it for reuse.
3. **Compiler — `if`/`else if`/`else`.** Emit condition + `JumpIfFalse`, the
   then-block, the unconditional `Jump` past `else`, and the `else`/`else if`
   chain, all backpatched through `patchJump`. Block scoping and `PopN` for each
   branch's locals reuse Increment A of Sprint 3.
4. **Compiler — `&&` / `||`.** Short-circuit via `JumpIfFalse` / `JumpIfTrue`
   with the operator table's exact stack discipline. No dedicated opcode.
5. **Compiler — ternary.** Jump-based, exactly one arm executes, result type is
   the unified arm type.
6. **Diagnostics via `ErrorCatalog` (D-308).** Every diagnostic raised through a
   catalog descriptor; no `"Exxxx"` literal at a call site; no invented codes.
   If a conditional diagnostic needs a code that does not exist, stop and
   surface for an assignment.

No new opcodes (the enum is closed), no parser or AST edits, no VM dispatch
changes (the jump opcodes already execute from Sprint 3 D).

## Out of scope

`while`, the backward `Loop` jump, `break`/`continue` (Increment B). `for...in`
(Increment C). `select`/`case` (Increment D). The switch expression (Increment
E). Flow-sensitive narrowing inside an `if (x != nil)` body — that is Sprint 5;
this increment does not narrow nullables. Do not edit the parser, the AST or the
`OpCode` enum. If you believe you need an opcode that is not there, stop and
surface.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - A non-`bool` `if` condition, `&&`/`||` operand and ternary condition are each
    a compile error with a source location.
  - Ternary arms that do not unify are a compile error; arms that unify give the
    ternary the unified `ResolvedType`.
  - `if`/`else if`/`else` disassembles to condition + `JumpIfFalse` + then-block
    + `Jump`-past-else + else-block, with every jump offset landing on the
    intended instruction (verify the backpatch targets through the disassembler,
    not only the VM's answer).
  - `a && b` and `a || b` disassemble to the short-circuit jump shape — confirm
    no dedicated opcode is emitted and the stack discipline matches the operator
    table.
  - `cond ? a : b` disassembles to the jump-based two-arm shape; exactly one arm
    is reachable per condition value.
  - §3.1.1 invariant: every identifier node in a type-checked conditional tree
    carries a non-null `ResolvedType` and a non-null `Declaration` (sentinels by
    reference, `Assert.Same`).
- **VM tests (`Grob.Vm.Tests`):**
  - A chunk for `if (true) print(1) else print(2)` and its `false` form execute
    the correct branch; a `false &&` short-circuit does not evaluate the right
    operand (use a right operand with an observable effect); a `true ||`
    likewise short-circuits.
  - A ternary yields the correct arm's value for each condition value.
- **Integration tests (`Grob.Integration.Tests`):**
  - A small script using `if`/`else if`/`else`, `&&`/`||` and a ternary runs
    end-to-end (via `grob run` or the integration harness) and produces the
    expected stdout.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `if`/`else if`/`else`, `&&`/`||` short-circuit and the ternary work; non-`bool`
  conditions/operands and non-unifying ternary arms are compile errors with
  source locations.
- Every jump offset is backpatched through the reused `patchJump` helper and
  lands correctly (verified by disassembly).
- The §3.1.1 invariant holds for every identifier node introduced.
- No new opcodes, no parser/AST edits, no `Loop` usage; the DAG holds.
- Diagnostics raise through `ErrorCatalog` descriptors; no literals, no invented
  codes.

## Model

Sonnet 4.6 (High) throughout. The bool-condition and arm-unification rules and
the jump emission are settled by the rules inlined above and the helper already
exists — this is transcription over a built primitive, not judgement. There is
no Opus carve-out in this increment.

## Hand-off

Summarise: how `if`/`else if`/`else` chains backpatch to a common exit, the
`&&`/`||` short-circuit stack discipline as built, the ternary arm-unification
and emission (and that it is shaped for the switch expression to reuse), the
diagnostics raised and their catalog codes, and the test files added. Note for
the next chat: Increment B is `while` + `break`/`continue` — the first backward
`Loop` jump and the loop-context stack that `break`/`continue` resolve against,
with the `break`/`continue`-outside-a-loop compile errors at **E2211**/**E2212**
(the next free in the E22xx statement-context block).
