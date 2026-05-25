---
name: "Sprint 2 · Increment B — VM Dispatch Loop"
description: "Build the stack-based fetch-decode-execute loop in Grob.Vm — ValueStack, arithmetic execution, Print and Return — proven against the same hand-constructed chunks the disassembler already reads, before the compiler exists. The #if DEBUG TraceInstruction hook (D-306) lands here."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 2 · Increment B — VM Dispatch Loop

You are continuing Sprint 2 of the Grob compiler. Increment A delivered the
back-end primitives (`OpCode`, `GrobValue`, `Chunk` in `Grob.Core`) and the
always-compiled `Disassembler` in `Grob.Vm`, proven against hand-constructed
chunks. This increment builds the **VM dispatch loop** — the
fetch-decode-execute engine that runs those chunks.

It is built **before the compiler exists**, executing the same
hand-constructed chunks the disassembler already prints. This is deliberate
and load-bearing. When the compiler arrives in Increment D, a wrong
`print(...)` result must have exactly one suspect. Building the loop now,
against bytecode you wrote by hand and verified by eye through the
disassembler, means the VM is independently proven to execute correct
bytecode correctly. The disassembler shows what the bytecode *is*; this
increment proves what the VM *does* with it. Build the compiler first instead
and every end-to-end bug has two suspects at once — which is the bisection
problem D-306 exists to prevent.

Read, in order:

1. `docs/design/grob-v1-requirements.md` §4 (Sprint 2 scope and acceptance —
   the VM bullet list) and §3.3 (the OpCode enum, for the arithmetic and
   stack semantics).
2. `docs/design/grob-vm-architecture.md` — the dispatch-loop, value-stack and
   `GrobValue` sections, and **"Developer Diagnostics"** for the
   `TraceInstruction` spec.
3. `docs/design/grob-solution-architecture.md` — `Grob.Vm` responsibilities
   and the Compiler/Vm separation (the DAG: `Grob.Vm` references `Grob.Core`
   and `Grob.Runtime`, never `Grob.Compiler`).
4. Decisions **D-303** (GrobValue), **D-306** (tracing), **D-304** (lean on
   .NET GC) in `docs/design/grob-decisions-log.md`.

> **Sequencing note.** This is Increment B of the agreed Sprint 2 breakdown:
> A (primitives + disassembler) → **B (dispatch loop, hand-built chunks)** →
> C (type checker) → D (compiler + end-to-end + benchmark skeleton). The
> dispatch-loop-before-compiler ordering is the deliberate call. Do not pull
> any type checker or compiler work forward into this increment.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name
   `feat/vm-dispatch-loop`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprint 1 (Increments A–E):** complete `TokenKind` + `Token`, full lexer,
  AST hierarchy with first-class `ErrorExpr`/`ErrorStmt`/`ErrorDecl`,
  error-recovering recursive-descent parser, recovery acceptance suite. All
  tests green.
- **Sprint 2 Increment A:** `OpCode` enum (complete from §3.3), `GrobValue`
  (D-303 tagged union), `Chunk` with its write surface — all in `Grob.Core`.
  `Disassembler` in `Grob.Vm`, always compiled, proven against
  hand-constructed chunks including `2 + 3 * 4`.
- `Grob.Core` foundations: `SourceLocation`, `SourceRange`, `Severity`,
  `Diagnostic`, `DiagnosticBag`.

## Deliverable for this increment

The fetch-decode-execute loop and its runtime state, in `Grob.Vm`. Proven
against hand-constructed chunks — the same construction surface Increment A's
disassembler tests already use. **No type checker, no compiler** — those are
Increments C and D.

1. **`ValueStack`.** The operand stack of `GrobValue`. Push, pop, peek. A
   fixed-capacity backing store (the VM architecture doc specifies the
   shape — follow it) with a clear overflow path that surfaces as a runtime
   error, not an unguarded array write. Pushing a primitive `GrobValue` is a
   struct copy with no allocation (D-303, D-304) — do not box.
2. **The dispatch loop.** A fetch-decode-execute `switch` over the full
   `OpCode` set *that this increment implements* — see the scope boundary
   below. Reads the bytecode byte array from the `Chunk`, advances the
   instruction pointer, dispatches. The full `OpCode` enum already exists
   from A; this increment wires up the subset needed to execute the
   Increment A chunks and reach the Increment D acceptance test.
3. **Arithmetic execution.** The typed arithmetic opcodes
   (`AddInt`/`AddFloat`/`Concat`, and the `Sub`/`Mul`/`Div` family per
   §3.3) — pop operands, compute, push result. No runtime type checks: the
   opcode already encodes the type (the type checker will guarantee this in
   C; for now the hand-built chunks supply correct typed opcodes). Checked
   arithmetic — `int` overflow and divide-by-zero throw the runtime errors
   the error model specifies (`ArithmeticError`), carrying the source line
   from the chunk's line array.
4. **`Constant` load, `Print`, `Return`.** `Constant` pushes a pooled
   `GrobValue`. `Print` pops and writes the value's display form to a
   supplied writer (not hard-wired `Console` — same discipline as the
   disassembler, so tests capture output and Increment D / the CLI can pass
   stdout). `Return` ends execution of the chunk.
5. **Runtime error surface.** A runtime error carries the source line from
   the chunk's per-instruction line array and is raised through the error
   type the model specifies. The VM stops on the **first** runtime error
   (the two-mode model: compiler/checker collect all, VM stops on first —
   the runtime half is what this increment implements).
6. **`#if DEBUG` `TraceInstruction` hook (D-306).** A `TraceInstruction(chunk,
   ip)` call at the top of the dispatch loop, guarded by `#if DEBUG` in the
   `Grob.Vm` C# source. Prints the value stack and the about-to-execute
   instruction every iteration, reusing the `Disassembler`'s
   `disassembleInstruction` for the instruction half. **Not** a runtime flag,
   **not** a CLI flag — compiled out of Release entirely so the Release
   dispatch loop the D-302 benchmarks measure stays branch-free. This is the
   loop the A prompt's hand-off named as the hook's home.

## Out of scope

No type checker (Increment C). No compiler (Increment D). No call frames,
no `Call`/`Return`-with-frame, no closures — those are Sprint 5; this
`Return` ends the top-level chunk only. No control-flow opcodes
(`Jump`/`JumpIfFalse`/`Loop`) — Sprint 4. No globals table, no
`DefineGlobal`/`GetGlobal` — that arrives with declarations in Sprint 3.
Implement only the opcode subset needed to run the Increment A chunks and
reach `print(2 + 3 * 4)` → `14`. The full enum exists; the full dispatch
does not yet. Do not touch `Grob.Compiler`.

## Tests (in `Grob.Vm.Tests`)

- Hand-construct the `2 + 3 * 4` chunk (reuse the Increment A construction
  surface) and assert the VM executes it to `14` on the stack / via `Print`.
- Each arithmetic family: `AddInt`/`AddFloat`/`Concat`, subtraction,
  multiplication, division — correct results, and `int / int` truncates per
  the numeric rules.
- `int` overflow throws `ArithmeticError` with the correct source line from
  the chunk line array.
- Divide-by-zero throws `ArithmeticError` with the correct source line.
- `Print` writes the expected display form to the supplied writer for each
  scalar kind.
- `ValueStack` overflow surfaces as a runtime error, not an unguarded write.
- A chunk that ends without `Return` — assert the defined behaviour (decide
  it and document it; do not leave it undefined).

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- The dispatch loop lives in `Grob.Vm`; the DAG holds (`Grob.Vm` references
  `Grob.Core` and `Grob.Runtime`, not `Grob.Compiler`).
- The VM executes the hand-constructed `2 + 3 * 4` chunk to `14`.
- Arithmetic errors stop execution on the first occurrence and carry the
  correct source line.
- `TraceInstruction` is present and compiles in Debug, is absent from Release
  (verify the Release build has no trace branch on the dispatch path).
- No allocation on the push path for primitive values.

## Model

Sonnet for the stack and the mechanical opcode arms. Reach for Opus if the
dispatch-loop structure, the trace-hook integration, or the
checked-arithmetic / source-line plumbing needs a judgement call — the loop
shape is the one place in this increment where a design decision (e.g.
computed-goto-style dispatch vs a plain `switch`, and how the ip is threaded)
has downstream consequences worth thinking through deliberately.

## Hand-off

Summarise: the `ValueStack` shape and overflow behaviour, the opcode subset
implemented vs deferred, the dispatch-loop structure chosen and why, the
arithmetic/source-line plumbing, the `Print` display forms, the
end-without-`Return` decision, the `TraceInstruction` integration and how you
verified it is absent from Release, and the test files added. Note for the
next chat: Increment C is the type checker — first AST visitor pass, sets
`ResolvedType` and `Declaration` on every identifier node (§3.1.1), collects
all errors, emits nothing. It annotates the tree that Increment D's compiler
will read to choose typed opcodes.
