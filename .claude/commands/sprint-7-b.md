---
description: "Sprint 7 · Increment B — `try`/`catch`. Emit TryBegin/TryEnd, build the handler table, polymorphic source-order first-match, catch-all-last, the immutable catch binding, the catch compile errors (E2204, E2205, the new catch-type code E0015, the new duplicate-catch code E2213), and the VM unwind-to-nearest-matching-handler arm. No finally."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 7 · Increment B — `try` / `catch`

Increment A made the language raise an exception and propagate it to the top level.
This increment makes it **recover**: `try { }` opens a protected region marked by
`TryBegin`/`TryEnd`, a handler table maps each region to its ordered catch handlers,
and the VM's `Throw` arm — extended here — unwinds to the nearest enclosing region
with a **matching** handler instead of always to the top level. Typed catches match
polymorphically in source order, the bare `catch e` catch-all matches everything not
matched above and must appear last, and the catch binding is in scope inside the
handler with its declared type. No `finally` yet — that is Increment C.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 7 — Error Handling** scope
   (the `try`/`catch` and typed-catch bullets, the `TryBegin`/`TryEnd` opcodes and
   the handler-table description), §3.3 (confirm the exception opcodes; **`TryBegin`
   and `TryEnd` are emitted here for the first time, the enum is not grown**), §3.5.
2. `docs/design/grob-language-fundamentals.md` — **§27** `try`/`catch` subsection
   (the grammar, the matching semantics, the binding, the permissiveness rule) and
   the hierarchy diagram (matching is over the subtype relationship A registered).
3. `docs/design/grob-grobc-format.md` — the handler-table / protected-region section
   of the `.grobc` format. Confirm how a region's bounds and its catch handlers are
   encoded so the emission here matches the format skeleton (D-298).
4. `docs/design/grob-error-codes.md` — confirm **E2204** (`try` without `catch` or
   `finally`) and **E2205** (`catch` after catch-all) exist with their descriptors,
   and grep for the **next free Type-block** number (the catch-type code, provisionally
   **E0015**) and the **next free Syntax-block** number (the duplicate-catch code,
   provisionally **E2213**). Do not take the numbers from this prompt.
5. Decisions: confirm **D-274** (the `try`/`catch` grammar, `catch (e: T)` typed form,
   `catch e` catch-all, `catch (e)` parens-no-type is a syntax error, polymorphic
   matching), **D-083** (catch-all last, catch-after-catch-all is a compile error),
   **D-284** (the hierarchy matching is over), **D-325** (location-based upvalue
   closing on unwind) and **D-308** (`ErrorCatalog`). Grep the log.

> **Verify before relying on cited decisions and sections.** Grep to confirm §27, the
> handler-table format and D-274/D-083 read as this prompt assumes. If a section has
> moved or a decision been superseded, surface it rather than proceeding. Confirm the
> `try`/`catch` productions parse to `TryStatement` with ordered `CatchClause`s (the
> A grammar-first gate should already have established this).
>
> **`try`/`catch` rules — inline reference (authoritative source is §27, D-274, D-083
> and `grob-grobc-format.md`; reproduced here so the implementation does not depend on
> a fetch landing well).**
>
> - **Protected regions and the handler table.** `try { }` compiles to a `TryBegin`
>   (operand: handler-table index) at the region's start and a `TryEnd` at its close.
>   The handler table entry records the region's bytecode bounds and its **ordered**
>   list of catch handlers, each carrying its catch type (a `GrobError` subtype, or
>   *null* for the catch-all) and the bytecode offset of its handler body. The
>   encoding matches the `.grobc` handler-table skeleton (D-298); confirm the field
>   layout before emitting.
> - **Grammar and the compile errors.** `try { }` must be followed by at least one
>   `catch` or a `finally` — a `try` with neither is **E2204**. Typed catch is `catch
>   (<n>: <Type>) { }`; the catch-all is `catch <n> { }` — identifier only, no parens,
>   no colon. `catch (e)` (parens, no type) is a syntax error (D-274) — confirm it
>   surfaces through parser recovery (D-300) with a clear message; add a dedicated
>   code only if recovery misleads. The catch-all is optional, at most one per `try`,
>   and must appear after all typed catches — a catch after the catch-all is **E2205**
>   (D-083). A typed catch whose type is **not** `GrobError` or a subtype (`catch (e:
>   int)`) is the **new catch-type code** (provisionally **E0015**, confirmed
>   next-free, Type block, dedicated over folding into E0001). A duplicate typed catch
>   for the same type is the **new duplicate-catch code** (provisionally **E2213**,
>   confirmed next-free, Syntax block) — this is an `allocating-an-error-code` (D-331)
>   fold-vs-new call: lean dedicated, because "catch for `IoError` already declared"
>   is distinct and more actionable than E2205's "catch after catch-all".
> - **Matching semantics.** On a `throw`, the VM finds the nearest enclosing protected
>   region on the call stack, then tries that region's catch handlers **in source
>   order**; the first whose type the thrown value is assignable to (its type or a
>   subtype, over the A hierarchy) wins; the catch-all (null type) matches anything.
>   If no handler in the nearest region matches, unwind to the next enclosing region;
>   if none matches anywhere, propagate to the top level (the A path). `catch (e:
>   GrobError) { }` is legal and behaves as a catch-all, but the bare `catch e { }`
>   form is preferred. In v1 the hierarchy is flat and closed, so polymorphic matching
>   is observably identical to exact matching in every legal program — build it
>   polymorphically anyway (D-274) so user exceptions slot in post-MVP.
> - **The permissiveness rule.** The type checker does **not** compute the can-throw
>   set of the try block. `catch (e: T)` is accepted even if nothing in the try block
>   can throw `T` (the C# model). No unreachable-catch analysis in v1.
> - **The binding.** Inside a catch body, the binding (`e`) is in scope with the
>   declared type — or `GrobError` for the catch-all. It is **immutable**: reassigning
>   it is a compile error (reuse the existing readonly-binding mechanism; the binding
>   is a fresh readonly local). The thrown exception value is placed in that slot by
>   the VM when the handler is entered.
> - **The VM unwind-to-handler arm.** Extend A's `Throw` arm: instead of always
>   unwinding to the top level, walk protected regions from innermost, match, and on a
>   match unwind the value stack and any call frames down to the handler's frame,
>   close open upvalues by location on every torn-down frame (D-325, route-agnostic),
>   bind the exception into the catch slot, and jump to the handler body. The value
>   stack must be exactly the handler frame's stack plus the bound exception — no
>   underflow, no leaked operands from the abandoned try body.
> - **No `finally`.** This increment builds catch matching only. It does not emit a
>   `finallyOffset`, does not run a finally on any path and does not enforce E2206 or
>   E2207. If you reach to handle a `finally` clause, stop — that is Increment C.
>
> **Sequencing note.** This is Increment B: A (hierarchy + throw + unhandled) →
> **B (try/catch)** → C (finally) → D (runtime-throw catchability + uncatchable exit)
> → E (close). Do not pull finally, the runtime-site routing or the smoke script
> forward.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered implementation plan — the
   `TryBegin`/`TryEnd` emission, the handler-table build and its `.grobc` encoding,
   the catch compile errors (E2204, E2205, E0015, E2213), the polymorphic
   source-order matcher, the immutable binding, the VM unwind-to-handler arm and the
   tests — and wait for Chris's approval before editing. The plan is the gate.
2. On approval, run `/start-branch` and propose `feat/try-catch`. Wait for Chris to
   create the branch.
3. Implement with TDD — tests written and confirmed **failing** first. Follow the
   `tdd-cycle` skill.
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–5** as summarised in the Increment A prompt.
- **Sprint 6:** user-defined types, the `NewStruct` construction path, field access
  and assignment, anonymous structs, the field-cycle check. Base error-code count 112.
- **Sprint 7 A:** the `GrobError` hierarchy registered as built-in nominal types with
  D-274 fields and leaf `<: GrobError` subtyping; `throw` type-checked (throw-operand
  code) and emitting `Throw`; exception construction reusing `NewStruct`; the VM
  `Throw` arm unwinding to the top level with the quality diagnostic and exit 1.
  Count 112 → 113. All green.

## Deliverable for this increment

`try`/`catch` working end-to-end — protected regions, the handler table, polymorphic
matching, the catch binding, the catch compile errors and the VM unwind-to-handler
arm. **No `finally`.**

1. **`TryBegin`/`TryEnd` emission and the handler table.** Each `try` region emits
   the region markers and a handler-table entry with its bounds and ordered catch
   handlers, encoded per the `.grobc` skeleton (D-298).
2. **Catch compile errors.** E2204 (`try` without `catch` or `finally`), E2205
   (`catch` after catch-all, D-083), the catch-type code (provisionally E0015) and
   the duplicate-catch code (provisionally E2213), all through `ErrorCatalog`
   descriptors. `catch (e)` parens-no-type confirmed via parser recovery.
3. **Polymorphic source-order matching.** The matcher tries handlers in source order,
   first-match wins over the subtype relationship, the catch-all matches anything and
   the permissiveness rule holds (no can-throw analysis).
4. **The immutable catch binding.** The thrown value is bound into a fresh readonly
   local in the handler body with the declared type (or `GrobError` for the
   catch-all); reassignment is a compile error. §3.1.1 holds on every node introduced.
5. **The VM unwind-to-handler arm.** Extends A's `Throw` arm to unwind to the nearest
   matching handler, closing upvalues by location (D-325), with an exact value stack
   and no underflow; falls through to the A top-level path when nothing matches.
6. **The new codes, registered in lockstep.** The catch-type and duplicate-catch
   codes registered at their next-free numbers in three-location lockstep, count
   reconciled (113 → 114 or 115 depending on the duplicate-catch fold-vs-new call),
   D-316 gate green.

No new opcodes, no parser/AST edits, no `finally`.

## Out of scope

`finally`, the `finallyOffset` handler-table field and the emitted finally chains
(Increment C). Routing the existing runtime throw sites through the exception model
and the uncatchable-`exit()` verification (Increment D). The smoke script and the
benchmark (Increment E). Do not edit the `OpCode` enum. If you reach for a `finally`
clause, stop — B is catch only.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker tests (`Grob.Compiler.Tests`):**
  - E2204 on `try { }` with neither catch nor finally; E2205 on a catch after the
    catch-all; the catch-type code on `catch (e: int)`; the duplicate-catch code on
    two `catch (e: IoError)` clauses; `catch (e)` parens-no-type surfaces a clear
    parser-recovery diagnostic.
  - `catch (e: GrobError) { }` type-checks as a catch that matches everything;
    reassigning the catch binding is a compile error.
  - The permissiveness rule: `catch (e: IoError)` on a try block that cannot throw
    `IoError` type-checks without error.
- **Compiler / disassembler tests (`Grob.Compiler.Tests`):**
  - A `try`/`catch` disassembles to `TryBegin` (with the right handler-table index),
    the try body, `TryEnd`, and the handler bodies, with the handler table recording
    the ordered catch types and offsets — verified through the disassembler.
- **VM tests (`Grob.Vm.Tests`):**
  - A typed catch catches a matching `throw` and binds the value; a non-matching typed
    catch does not; the catch-all catches anything not matched above; source order
    decides first-match; an unmatched throw falls through to the top-level path.
  - A `throw` from a nested call frame inside a `try` unwinds the frames to the
    handler, closes upvalues by location (D-325) and leaves an exact value stack with
    no underflow.
  - Per-region independence: a throw in an inner `try` matched there does not disturb
    an outer `try`'s region.
  - Layer-invariant `[Theory]` rows: pathological but parseable `try`/`catch` shapes
    type-check to a result or a diagnostic, never throw a host exception.
- **Integration / spec-consistency:** the consistency gate (D-316) is green;
  catalog↔registry agreement holds; the count is reconciled.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `try`/`catch` works: typed catches match polymorphically in source order, the
  catch-all catches everything not matched above, unmatched throws propagate to the
  top level.
- The catch compile errors fire correctly (E2204, E2205, the catch-type code, the
  duplicate-catch code); the catch binding is immutable.
- The VM unwind-to-handler arm leaves an exact value stack, closes upvalues by
  location and does not underflow.
- The §3.1.1 invariant holds for every node introduced.
- No new opcodes, no parser/AST edits, no finally; the DAG holds.
- Diagnostics raise through `ErrorCatalog` descriptors; no literals, no invented
  codes; the count is reconciled.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. The grammar, the matching semantics and the handler-
table encoding are settled by §27, D-274/D-083 and the `.grobc` skeleton; the unwind
extends A's arm over the D-325 mechanism already in place. This is a bounded
type-check, a specified emission and a stack-discipline extension — not a load-bearing
structural decision. There is no Opus carve-out in this increment. If the value-stack
discipline on unwind turns out to hide a genuine structural question, stop and
surface it rather than reaching for Opus.

## Hand-off

Summarise: the handler-table entry shape as built and its `.grobc` encoding, the
polymorphic source-order matcher, the catch compile errors and their codes, the
immutable-binding mechanism, the VM unwind-to-handler arm and its value-stack
discipline, the new codes and their lockstep registration, and the test files added.
Note for the next chat: Increment C is `finally` — the load-bearing increment. It adds
the handler-table `finallyOffset` for the exceptional path (D-275) and the
compiler-emitted finally chains for the non-exceptional paths (D-332), enforces E2206
(finally not last) and E2207 (control flow in finally), handles throw-in-finally
replacing the in-flight exception, and carries the Opus subagent carve-out on the
finally-emission-chain sub-problem.
