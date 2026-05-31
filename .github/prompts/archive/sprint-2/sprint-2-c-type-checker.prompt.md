---
name: "Sprint 2 · Increment C — Type Checker"
description: "Build the first AST visitor pass in Grob.Compiler — two-pass (D-166), type inference on :=, arithmetic and comparison type rules, ResolvedType and Declaration back-reference on every identifier node (§3.1.1), collect-all-errors. Annotates the tree; emits no bytecode."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 2 · Increment C — Type Checker

You are continuing Sprint 2 of the Grob compiler. Increments A and B
delivered the back-end: primitives and disassembler (A), and a VM dispatch
loop proven against hand-constructed chunks (B). This increment builds the
**type checker** — the first AST visitor pass — which annotates the parsed
tree so the compiler (Increment D) can choose typed opcodes from type
information rather than discovering types at runtime.

The type checker **emits no bytecode**. Its job is to walk the AST, infer and
validate types, decorate identifier nodes with their resolved type and a
back-reference to their declaration, and collect every type error into the
diagnostic bag without stopping at the first. The compiler reads the result.

Read, in order:

1. `docs/design/grob-v1-requirements.md` §4 (Sprint 2 scope — the type
   checker bullet list), §3.1.1 (LSP-enabling properties — `ResolvedType`,
   `Declaration`, `DeclaredAt`, day-one), and §3.2 (diagnostic
   infrastructure).
2. `docs/design/grob-language-fundamentals.md` — the type-rules sections:
   inference on `:=`, the arithmetic type rules, comparison rules, and the
   two-pass declaration-registration model. Grep for the section that covers
   forward references and top-level declaration registration.
3. `docs/design/grob-type-registry.md` — `GrobType`, the built-in scalar
   types (lowercase per D-307), and how types are represented for the
   registry the checker populates and queries.
4. `docs/design/grob-solution-architecture.md` — `Grob.Compiler`
   responsibilities, the visitor pattern over the AST, and the DAG
   (`Grob.Compiler` references `Grob.Core`, never `Grob.Vm`).
5. Decisions **D-166** (two-pass type checker), **D-307** (built-in type
   names lowercase) in `docs/design/grob-decisions-log.md`.

> **Verify before relying on cited decisions.** This prompt cites D-166 and
> D-307. Grep the decisions log to confirm both say what this prompt assumes
> (D-166 = two-pass registration enabling forward references; D-307 =
> built-in scalar type names are lowercase) before building on them. If
> either has been superseded or renumbered, surface it rather than proceeding.
>
> **Type rules — inline reference (authoritative source is the fundamentals
> spec; reproduced here so the implementation does not depend on a fetch
> landing well).** These are the rules the checker resolves and Increment D's
> compiler turns into opcode choice:
>
> - `int op int → int`
> - `float op float → float`
> - `int op float → float` (implicit `int → float` promotion — the _only_
>   implicit conversion in Grob)
> - `int / int → int` (truncating)
> - `string + string → string`
> - `int + string`, and every other mixed combination not listed above, is a
>   compile error with a clear diagnostic and a source location
> - comparisons resolve to `bool`; validate operand compatibility per the
>   fundamentals spec
>
> Resolve the operation type _precisely_ (not merely "numeric") — `AddInt`
> vs `AddFloat` vs `Concat` in Increment D is selected directly off this
> resolved type.
>
> **Sequencing note.** This is Increment C of the agreed Sprint 2 breakdown:
> A → B → **C (type checker)** → D (compiler + end-to-end + benchmark
> skeleton). The type checker annotates; it does not emit. Do not pull any
> opcode emission forward into this increment — that is Increment D, and
> keeping the boundary clean is what lets D be a focused emit-from-annotations
> pass.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name
   `feat/type-checker`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprint 1 (Increments A–E):** complete front end — `TokenKind` + `Token`,
  lexer, AST hierarchy with first-class `ErrorExpr`/`ErrorStmt`/`ErrorDecl`,
  error-recovering parser, recovery acceptance suite. All tests green.
- **Sprint 2 Increment A:** `OpCode`, `GrobValue` (D-303), `Chunk` in
  `Grob.Core`; `Disassembler` in `Grob.Vm`.
- **Sprint 2 Increment B:** VM dispatch loop in `Grob.Vm` — `ValueStack`,
  arithmetic execution, `Constant`/`Print`/`Return`, checked arithmetic with
  source lines, `#if DEBUG` `TraceInstruction`. Proven against
  hand-constructed chunks.
- `Grob.Core` foundations: `SourceLocation`, `SourceRange`, `Severity`,
  `Diagnostic`, `DiagnosticBag`. The AST nodes carry `SourceLocation` and
  identifier nodes carry the (as-yet-unset) `ResolvedType` and `Declaration`
  fields from §3.1.1.

## Deliverable for this increment

The type checker as an AST visitor in `Grob.Compiler`, two-pass per D-166,
collecting all diagnostics into the bag. **No bytecode emission.**

1. **Two-pass structure (D-166).** Pass 1 registers all top-level
   declarations (types and functions) into the type checker's symbol
   table / type registry. Pass 2 validates bodies. This ordering is what
   lets a body reference a type or function declared later in the same file.
   Pass 1 sets `DeclaredAt` on each `Symbol` (§3.1.1).
2. **Type inference on `:=`.** Infer the declared binding's type from its
   right-hand-side expression. Register the binding's name and inferred type
   in the active scope.
3. **Explicit annotation validation.** Where a binding or parameter carries
   an explicit type annotation, validate the right-hand side / usage against
   it.
4. **Arithmetic type rules.** `int op int → int`, `float op float → float`,
   `int op float → float` (implicit promotion — the only implicit
   conversion), `string + string → string`, `int / int → int` (truncating).
   `int + string` and the other illegal mixes are compile errors with clear
   diagnostics and source locations. These rules are what tell Increment D's
   compiler which typed opcode to emit (`AddInt` vs `AddFloat` vs `Concat`),
   so the checker must resolve the operation type precisely, not just
   "numeric".
5. **Comparison type rules.** Per the fundamentals spec — resolve the result
   type (`bool`) and validate operand compatibility.
6. **`ResolvedType` on every identifier node (§3.1.1).** Set `GrobType
ResolvedType` on each identifier node during the pass. Non-null after
   type-checking is the invariant.
7. **`Declaration` back-reference on every identifier node (§3.1.1).** Set
   `AstNode? Declaration` pointing to the node where the name was declared.
   This is what drives LSP go-to-definition later; it is set now, day-one,
   not retrofitted.
8. **Collect ALL errors.** Never stop at the first type error. Accumulate
   into the `DiagnosticBag` (§3.2) and report after the pass. This is the
   compile-time half of the two-mode error model — contrast Increment B's
   VM, which stops on the first runtime error.
9. **Error type and cascade suppression.** Where an expression's type cannot
   be resolved because a sub-expression already errored, resolve it to the
   compiler-internal `Error` type (universally assignable) so a single
   mistake does not cascade into a storm of downstream diagnostics. The
   `Error` type and its universal-assignability contract are in the type
   registry — reuse them, do not invent a parallel mechanism.

## Out of scope

No bytecode emission — that is Increment D. No control-flow type rules
beyond what bare expressions and `:=` need (`if`/`while`/`for`/`select`
type-checking is Sprint 4). No function-call or lambda type-checking beyond
top-level registration (arity and argument checking is Sprint 5). No struct
or anonymous-struct type-checking (Sprint 6). No optional narrowing (Sprint
5). Implement the type rules needed for the Sprint 2 surface — scalar
arithmetic, comparisons, `:=` inference, annotation validation — plus the
two-pass registration scaffold that later sprints extend. Do not touch
`Grob.Vm`.

## Tests (in `Grob.Compiler.Tests`)

- Inference: `x := 2 + 3` resolves `x` to `int`; `y := 2.0 + 3` resolves to
  `float` (implicit promotion); `s := "a" + "b"` resolves to `string`.
- Arithmetic rules: `int + string` produces a clear compile error with a
  source location; `int / int` resolves to `int`.
- `ResolvedType` invariant: after type-checking a representative tree, every
  identifier node carries a non-null `ResolvedType` (this is the §3.1.1
  verification the requirements call for).
- `Declaration` invariant: after type-checking, every identifier node carries
  a non-null `Declaration` pointing to the correct declaring node.
- Two-pass forward reference: a body referencing a top-level declaration that
  appears later in the file type-checks without error.
- Collect-all: a tree with three independent type errors reports all three,
  not just the first.
- Cascade suppression: an expression built on an already-errored
  sub-expression resolves to `Error` and does not emit a second, derived
  diagnostic.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- The type checker lives in `Grob.Compiler`; the DAG holds (`Grob.Compiler`
  references `Grob.Core`, not `Grob.Vm`).
- Every identifier node in a type-checked AST carries a non-null
  `ResolvedType` and a non-null `Declaration`.
- The checker collects all type errors before reporting; it never stops at
  the first.
- Two-pass registration enables forward references to later top-level
  declarations.
- No bytecode is emitted by this pass.

## Model

Sonnet 4.6 (High effort) throughout — including the visitor scaffolding, the
type-rule arms, and the two-pass registration. The trigger for reaching for
Opus is not "this part is hard"; it is "this is a structural decision later
sprints build on, and an awkward shape here is expensive to unwind." On that
test the one candidate in this increment is the `Error`-type
cascade-suppression contract: every later sprint's type-checking extends it,
so the propagation rule (one mistake suppresses derived diagnostics without
swallowing genuine independent errors) wants to be right once. Even there,
paste the `Error`-type universal-assignability section from
`grob-type-registry.md` into the prompt first — with the contract in front of
it, Sonnet likely does not need the upgrade. The arithmetic and comparison
arms are settled by the rules inlined above, so they are transcription, not
judgement, and stay on Sonnet.

## Hand-off

Summarise: the two-pass structure as built, the symbol-table / type-registry
shape the checker populates, the type rules implemented vs deferred, how
`ResolvedType` and `Declaration` are set and how the invariant is verified in
tests, the collect-all wiring into the `DiagnosticBag`, the cascade-
suppression behaviour via the `Error` type, and the test files added. Note
for the next chat: Increment D is the compiler — the second AST visitor pass.
It reads the `ResolvedType` annotations this increment set to choose typed
opcodes (`AddInt`/`AddFloat`/`Concat`), manages the constant pool, tracks
line numbers per instruction, and closes Sprint 2 with `print(2 + 3 * 4)` →
`14` end-to-end plus the BenchmarkDotNet skeleton (D-302).
