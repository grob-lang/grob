---
name: "Sprint 2 Kickoff"
description: "Open Sprint 2 of the Grob compiler — the type checker, compiler core, VM dispatch loop and back-end primitives. Records the agreed A–D increment breakdown and the B-before-D rationale. Start with Increment A."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 2 Kickoff

Begin Sprint 2 — the back end. Sprint 1 delivered the front end (solution
scaffold, lexer, diagnostics, error-recovering parser — all green). Sprint 2
turns parsed trees into running bytecode: the type checker, the compiler core,
the VM dispatch loop and the back-end primitives. The full scope and
acceptance are in `docs/design/grob-v1-requirements.md` §4 (Sprint 2).

> **This prompt is a record, not a gate.** Unlike the Sprint 1 kickoff, this
> one does no setup work and unblocks nothing. The solution scaffold already
> exists from Sprint 1, and the increment breakdown below was agreed in
> planning before this prompt was written. There is nothing to confirm before
> starting — **go straight to Increment A.** This document exists so the
> Sprint 2 planning decisions have a durable home alongside the Sprint 1
> kickoff, not because Increment A depends on it.

## The agreed increment breakdown

§4 of the requirements doc still describes Sprint 2 as a single monolithic
sprint with one acceptance block (`print(2 + 3 * 4)` → `14`). That is too
much for one increment. The work is sliced into four, on the clox ordering —
the data structure, then the tool that reads it, then the engine that runs
it, then the compiler that feeds it:

- **A — Primitives + disassembler.** `OpCode`, `GrobValue` (D-303), `Chunk`
  in `Grob.Core`; the always-compiled `Disassembler` in `Grob.Vm`, proven
  against hand-constructed chunks. Branch `feat/vm-opcode-chunk-and-disassembler`.
- **B — VM dispatch loop.** `ValueStack`, fetch-decode-execute, arithmetic
  execution, `Constant`/`Print`/`Return`, checked arithmetic, the `#if DEBUG`
  `TraceInstruction` hook (D-306). Against the same hand-constructed chunks,
  before the compiler exists. Branch `feat/vm-dispatch-loop`.
- **C — Type checker.** First AST visitor pass, two-pass (D-166), `:=`
  inference, arithmetic/comparison type rules, `ResolvedType` and
  `Declaration` on every identifier node (§3.1.1), collect-all-errors,
  `Error`-type cascade suppression. Annotates the tree; emits nothing. Branch
  `feat/type-checker`.
- **D — Compiler + end-to-end + benchmark skeleton.** Second AST visitor
  pass, typed-opcode selection from `ResolvedType`, constant pool, line
  tracking, `print()`/`exit()`, the end-to-end `14` integration test, and the
  BenchmarkDotNet skeleton with the first `compile.json` baseline (D-302).
  Closes the sprint. Branch `feat/compiler-and-end-to-end`.

Each increment has its own prompt (`sprint-2-{a,b,c,d}-*_prompt.md`). Run them
in order, each building and testing green before the next, a fresh chat per
increment.

## The one deliberate call: B before D

The natural instinct is to build the compiler straight after the type checker,
so the VM has something to run. Resist it. The dispatch loop (B) is built
*before* the compiler (D), executing hand-constructed chunks that the
disassembler (A) already prints and that you have verified by eye.

The reason is bisection. When the compiler arrives and an end-to-end
`print(...)` gives a wrong answer, that answer must have exactly one suspect.
With B already proven against hand-built bytecode, the VM is known to execute
correct bytecode correctly — so a wrong end-to-end result points at the
compiler's emission, and the disassembler localises it immediately. Build the
compiler first instead and every end-to-end bug has two suspects at once,
which is the precise problem D-306 exists to prevent. The A→B→C→D ordering is
not arbitrary; B-before-D is the load-bearing choice in it.

## Acceptance to hit (whole sprint)

The §4 acceptance, met across the four increments: `print(2 + 3 * 4)`
compiles, runs and prints `14`; type errors (e.g. `"hello" + 42`) produce
clear compile-time diagnostics with line numbers and do not execute;
`bench/Grob.Benchmarks` builds and the first compile-time baseline is
committed. Sprint 2 is closeable when Increment D's acceptance is green.

## Notes

- **Sequential, one increment per chat.** Fresh zip uploaded at the start of
  each increment as the known-good state, per the established working pattern.
- **§4 stays monolithic for now.** Whether to rewrite §4 to reflect the A–D
  structure, or leave the prompts as the finer-grained layer over a coarse
  requirements doc, is a documentation-authority call deferred to Sprint 2
  close — decide it once the four-way split has proven itself in practice.
- **Model:** per-increment guidance is in each increment's prompt. Broadly,
  Sonnet for mechanical work (primitives, emission arms, scaffolding); Opus
  for the judgement calls — the dispatch-loop structure in B, the
  two-pass/cascade-suppression design in C, the typed-opcode selection and
  compiler skeleton in D.
- Start with Increment A.
