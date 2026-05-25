---
name: "Sprint 2 · Increment A — Disassembler"
description: "Define OpCode, GrobValue and Chunk in Grob.Core, then build the always-compiled bytecode disassembler in Grob.Vm against hand-constructed chunks — the layer-boundary debugging tool, ready before the compiler emits anything."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 2 · Increment A — Disassembler

You are opening Sprint 2 of the Grob compiler. Sprint 2 delivers the type
checker, the compiler core, the VM dispatch loop and the back-end primitives.
This increment builds the **bytecode disassembler** first, against
hand-constructed chunks, so that every later increment that emits bytecode is
debuggable from the moment it emits.

Read, in order:

1. `docs/design/grob-v1-requirements.md` §3.3 (OpCode enum — complete) and §4
   (Sprint 2 scope and acceptance).
2. `docs/design/grob-vm-architecture.md` — the constant-pool and `Chunk`
   sections, and especially **"Developer Diagnostics"** (the disassembler and
   tracing spec).
3. `docs/design/grob-solution-architecture.md` — `Grob.Core` and `Grob.Vm`
   responsibilities and the Compiler/Vm separation.
4. Decision **D-306** in `docs/design/grob-decisions-log.md`.

> **Sequencing note.** Sprint 2's full increment breakdown is not yet locked.
> This is the proposed opening increment — primitives plus the disassembler —
> on the clox ordering (the data structure, then the tool that reads it,
> before the VM and the compiler). Confirm the breakdown with the maintainer
> before assuming Increments B+ exist.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name
   `feat/vm-opcode-chunk-and-disassembler`.
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
- `Grob.Core` foundations: `SourceLocation`, `SourceRange`, `Severity`,
  `Diagnostic`, `DiagnosticBag`.

## Deliverable for this increment

Two pieces — the back-end primitives in `Grob.Core`, then the disassembler in
`Grob.Vm`. **No type checker, no compiler, no VM dispatch loop yet** — those
are later Sprint 2 increments. The disassembler is built and proven against
chunks you construct by hand.

1. **`OpCode` enum in `Grob.Core` — complete from §3.3.** The full v1
   instruction set, written out, not grown incrementally (same discipline as
   `TokenKind`). Typed arithmetic opcodes (`AddInt`/`AddFloat`/`Concat` etc.)
   as the requirements specify.
2. **`GrobValue` in `Grob.Core` (D-303).** Hand-rolled `readonly struct`
   tagged union: `GrobValueKind` discriminator, `long _scalar` for primitives,
   `object? _reference` for heap types. Encapsulated factory/accessor surface.
   Only as much as the disassembler needs to print constants — the full
   accessor surface can fill in alongside the VM, but the shape is locked here.
3. **`Chunk` in `Grob.Core`.** Bytecode byte array, constant pool
   (`GrobValue[]` or equivalent), and the per-instruction source line array.
   Plus the minimal write surface a hand-constructed-chunk test needs
   (`writeByte`, `addConstant`, line tracking) — the compiler will use the
   same surface in a later increment.
4. **`Disassembler` in `Grob.Vm` — always compiled, not `#if DEBUG`.**
   - `disassembleChunk(Chunk)` — walks the whole chunk, instruction by
     instruction.
   - `disassembleInstruction(Chunk, int offset)` — disassembles one
     instruction and returns the offset of the next.
   - Each line: byte offset, opcode name, operands; for constant-bearing
     opcodes, the constant-pool index and its resolved value; source line
     number, with a marker when an instruction shares a line with the
     previous one (clox `|` convention or your chosen equivalent — document
     it).
   - Output goes to a writer the caller supplies (so tests capture it; the
     Sprint 12 `grob dump` will pass stdout). Do not hard-wire `Console`.

## Out of scope

No type checker. No compiler. No VM dispatch loop. No execution tracing yet —
the `#if DEBUG` `TraceInstruction` hook lands with the dispatch-loop increment,
not here (it has no loop to sit in). No `grob dump` CLI command — that is a
Sprint 12 wrapper over this engine. Do not touch `Grob.Compiler`.

## Tests (in `Grob.Vm.Tests`)

- Hand-construct a chunk for `2 + 3 * 4` (constants + typed arithmetic opcodes
  + `Return`) and assert the disassembled listing line by line.
- A chunk exercising a constant-bearing opcode, asserting the pool index and
  the resolved value both appear.
- The shared-line marker: two instructions on the same source line render the
  marker rather than repeating the line number.
- `disassembleInstruction` returns the correct next offset for fixed-width and
  operand-bearing opcodes.
- An unknown/invalid opcode byte disassembles to a clear "unknown opcode"
  line rather than throwing — the disassembler is a debugging tool and must
  survive malformed input.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `OpCode`, `GrobValue` and `Chunk` live in `Grob.Core`; `Disassembler` lives
  in `Grob.Vm`; the DAG holds (`Grob.Vm` references `Grob.Core`, not
  `Grob.Compiler`).
- `disassembleChunk` produces a correct, readable listing for a
  hand-constructed `2 + 3 * 4` chunk.
- The disassembler never throws on malformed bytecode.

## Model

Sonnet for the primitives (mechanical — enum, struct, data class). The
disassembler is mostly mechanical too; reach for Opus only if the
operand-decoding or output-format design gets fiddly.

## Hand-off

Summarise: the `OpCode` count and any §3.3 ambiguities resolved, the
`GrobValue` accessor surface as built so far, the `Chunk` write surface, the
disassembler's output format (including the shared-line marker convention),
and the test files added. Note for the next chat: the VM dispatch loop is
where the `#if DEBUG` `TraceInstruction` hook (D-306) lands.
