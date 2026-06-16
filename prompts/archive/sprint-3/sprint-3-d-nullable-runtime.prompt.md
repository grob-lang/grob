---
name: "Sprint 3 · Increment D — Nullable Runtime"
description: "Build the nullable runtime in Grob.Compiler and Grob.Vm — T? type rules, ?? (NilCoalesce, eager per the spec), ?. optional-chaining short-circuit via IsNil + jumps, and the forward-jump backpatching helper Sprint 4 reuses. No flow-narrowing — that is Sprint 5."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 3 · Increment D — Nullable Runtime

You are continuing Sprint 3. Increments A–C delivered mutable and
single-assignment bindings, the scope chain and `grob run`. This increment adds
nullability: the `T?` type rules, the `??` nil-coalescing operator
(`NilCoalesce`), the `?.` optional-chaining operator (short-circuiting), and the
`IsNil` opcode. It is the first increment that needs forward-jump backpatching —
`?.` must short-circuit and there is no `?.` opcode — so it builds the
`emitJump`/`patchJump` helper that Sprint 4's control flow reuses. Flow-sensitive
type narrowing (`if (x != nil) { … }`) is **Sprint 5**, not here.

Read, in order:

1. `docs/design/grob-v1-requirements.md` §4 (Sprint 3 — the nil/nullable
   bullets: `?` suffix, `??`, `?.`, `IsNil`, `NilCoalesce`), §3.3 (the `OpCode`
   enum — `IsNil`, `NilCoalesce`, `Nil` and the `Jump`/`JumpIfFalse`/
   `JumpIfTrue` family this increment first wires), §3.5 (test routing).
2. `docs/design/grob-language-fundamentals.md` — the nullable-type rules, the
   operator table and precedence for `??`/`?.`, and **§21** (Optional Chaining
   and Nil Propagation). Note what §21 requires of short-circuit behaviour.
3. `docs/design/grob-vm-architecture.md` — `GrobValue`'s nil representation, and
   the dispatch-loop / jump-offset mechanics.
4. `docs/design/grob-error-codes.md` — E0101 (nil dereference without `?.`/`??`),
   E0104 (nullable used where non-nullable required), E5201 (nil dereference at
   runtime).
5. Decisions **D-303** (`GrobValue`), **D-308** (`ErrorCatalog`) in the log;
   and check whether any decision governs `??` short-circuit behaviour (see the
   eager-`??` note below).

> **Verify before relying on cited decisions and on the eager-`??` reading.**
> Grep the decisions log for any entry governing `??` evaluation order before
> building.
>
> **`??` is EAGER, per the spec as written — inline reference (read this
> carefully; it is the one place this increment encodes a contested reading).**
> The `OpCode` enum defines `NilCoalesce` as "pop two, push right if left is
> nil" — both operands are evaluated and pushed before the opcode runs. The
> operator table annotates `&&` and `||` as short-circuiting but does **not**
> annotate `??`. So `a ?? b` evaluates **both** `a` and `b`; `b` is not skipped
> when `a` is non-nil. Implement `??` as the eager `NilCoalesce` opcode: compile
> `a`, compile `b`, emit `NilCoalesce`. **Do not** lower `??` to a jump-based
> short-circuit — that would contradict the opcode and the table.
>
> If the maintainer later decides `??` should short-circuit its right operand
> (the C# / JS / Swift behaviour), that is a `D-###` that reshapes this section
> and makes `NilCoalesce` partly vestigial. Until then, eager is the spec and
> this increment implements eager with no leeway. If your reading of the spec
> diverges from "eager", stop and surface before implementing — do not pick.
>
> **`?.` MUST short-circuit — inline reference (§21).** `a?.b?.c` evaluates to
> `nil` the moment any receiver in the chain is `nil`; no further member access
> or call is attempted, and the chain's static type is nullable (`T?`). There is
> **no** `?.` opcode. Compile each `?.` step as: evaluate the receiver; test it
> with `IsNil`; if nil, jump forward over the rest of the chain and leave `nil`
> on the stack; otherwise continue. This needs **forward-jump backpatching** —
> the jump target is not known until the rest of the chain is emitted. The
> `Jump`/`JumpIfTrue`/`JumpIfFalse` opcodes exist (the enum was closed in Sprint
> 2 A); the backpatching *mechanism* does not yet — build it here.
>
> **Backpatching helper — build it as a reusable compiler primitive.** Emit a
> jump with a placeholder 2-byte offset, record the patch site, then after
> emitting the skipped region patch the offset to the current position. Shape it
> (`emitJump(opcode) → patchSite`, `patchJump(patchSite)`) so Sprint 4's
> `if`/`while`/`&&`/`||` reuse it rather than reinvent it. Mirror the clox
> approach.
>
> **Sequencing note.** This is Increment D of the agreed breakdown: A → B → C →
> **D (nullable)** → E → F. No flow-narrowing — `if (x != nil)` narrowing of
> `x` from `T?` to `T` is Sprint 5, and so is the narrowing-based resolution of
> nullable interpolation. In Sprint 3, the only way to resolve a nullable for a
> non-nullable context is `?? <fallback>`. Do not pull narrowing forward, and do
> not implement general `if`/`while` control flow — only the jump-based skip
> that `?.` needs.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name `feat/nullable-runtime`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprint 1 & 2:** front end and back end complete; AST grammar-complete
  (confirm the `??`/`?.`/nil expression nodes in `Grob.Compiler/Ast/Expressions`
  before relying on their names — the parser is grammar-complete so they exist,
  but use the actual node names, not assumed ones). `Nil`, `IsNil`,
  `NilCoalesce` and the `Jump` family are in the closed `OpCode` enum.
  `ErrorCatalog` (D-308), C# 14 (D-310).
- **Sprint 3 A–C:** mutable declarations and assignment with the scope chain and
  slots; `grob run`; `const` (compile-time, inlined) and `readonly` (runtime-
  once, deep-immutable) with E0205/E0206 registered.

## Deliverable for this increment

Nullable types and operators across the type checker and compiler
(`Grob.Compiler`) and the VM (`Grob.Vm`), plus the reusable backpatching helper.

1. **Type checker — `T?` rules.** A `T?` type is nullable; a non-`?` type is
   guaranteed non-nil. Dereferencing a nullable without `?.`/`??` in a context
   that requires non-nil is **E0101**. Assigning a `T?` where `T` is required is
   **E0104**. `??` resolves `a ?? b` to the non-nullable element type when `b`
   is non-nullable. `?.` resolves the whole chain to `T?`.
2. **Compiler — `??` (eager `NilCoalesce`).** Compile `a`, compile `b`, emit
   `NilCoalesce`. Eager, per the inline reference. No jump-based lowering.
3. **Compiler — `?.` (short-circuit via jumps + backpatching).** Compile each
   chain step with `IsNil` and a forward jump over the remainder, leaving `nil`
   on the stack when a receiver is nil. Use the backpatching helper.
4. **Backpatching helper.** The reusable `emitJump`/`patchJump` primitive
   described above, shaped for Sprint 4 reuse.
5. **VM — opcode arms.** `IsNil` (push bool: is top nil?), `NilCoalesce` (pop
   two, push right if left is nil), `Nil` (push nil) and the `Jump`/
   `JumpIfTrue`/`JumpIfFalse` arms the `?.` lowering uses — implement exactly the
   jump arms this increment needs; general control-flow jumps are wired
   identically in Sprint 4, so build them correctly now (read the 2-byte offset,
   advance the ip). A nil dereference that reaches the VM is **E5201** with the
   source line.
6. **Diagnostics via `ErrorCatalog`.** E0101, E0104, E5201 already exist — raise
   them via their catalog descriptors. Do not invent codes; if a nullable
   diagnostic seems to need a code that does not exist, stop and surface.

## Out of scope

No flow-sensitive narrowing (`if (x != nil)` → `x: T`) — Sprint 5. No general
`if`/`while`/`for`/`select` control flow — Sprint 4 (this increment builds the
backpatching helper and the jump arms, but wires no control-flow statements). No
string-interpolation compilation (Increment E) — but note E's D-279 rule depends
on the `T?` resolution this increment provides. No `&&`/`||` short-circuit
(Sprint 4) — only `?.`. Do not edit the parser or AST, do not extend the
`OpCode` enum.

## Tests

- **Type-checker / compiler (`Grob.Compiler.Tests`):**
  - `x: int? := …` is nullable; dereferencing it bare where non-nil is required
    is E0101; assigning it to an `int` is E0104.
  - `a ?? b` compiles to `compile(a); compile(b); NilCoalesce` — assert the
    eager sequence, and assert there is **no** jump for `??`.
  - `a?.b` compiles to the `IsNil` + forward-jump skip; `a?.b?.c` produces the
    multi-step chain with each `?.` an independent nil check and a single nil
    result on short-circuit. Assert the backpatched offsets are correct.
  - The chain's resolved type is `T?`.
- **VM (`Grob.Vm.Tests`):**
  - `IsNil` on nil and non-nil; `NilCoalesce` picks left when non-nil, right when
    nil (eager — both already on the stack); a `?.` chain short-circuits to nil
    when an early receiver is nil and does not execute the later steps.
  - A `JumpIfTrue`/`Jump` round-trip advances the ip to the backpatched target.
  - A nil dereference reaching the VM raises E5201 with the correct source line.
- **Integration (`Grob.Integration.Tests`):**
  - `name := user?.field ?? "default"` runs end-to-end via `grob run` and prints
    the fallback when the receiver is nil and the value when it is not.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `T?` rules hold (E0101, E0104); `??` is eager (`NilCoalesce`, no jump); `?.`
  short-circuits the whole chain via `IsNil` + backpatched jumps.
- The reusable `emitJump`/`patchJump` helper exists and produces correct
  offsets, shaped for Sprint 4 reuse.
- The VM executes `?.`/`??` chains correctly; a runtime nil dereference is E5201
  with the source line.
- The §3.1.1 invariant holds across nullable trees; the DAG, parser, AST and
  `OpCode` enum are unchanged.

## Model

Sonnet 4.6 (High) throughout. The `T?` type rules and the eager `??` arm are
transcription against the inline reference. The one structural candidate is the
**forward-jump backpatching helper**: Sprint 4's `if`/`while`/`&&`/`||` and
everything jump-based after it reuse this primitive, so an awkward shape is
expensive to unwind. With the clox `emitJump`/`patchJump` model and §21's
short-circuit requirement inlined above in front of it, Sonnet handles it; reach
for Opus only if the multi-jump `a?.b?.c` chain backpatching (several forward
jumps converging on one nil-result point) needs a judgement call. The trigger is
"later sprints reuse this primitive", not "this part is hard".

## Hand-off

Summarise: the `T?` type rules implemented, the eager `??` emission (and an
explicit note that you implemented eager per the spec as written, flagging it so
the maintainer can convert to short-circuit via a `D-###` if wanted), the `?.`
short-circuit lowering, the `emitJump`/`patchJump` helper's shape and how Sprint
4 will reuse it, the VM jump and nil arms, the test files added. Note for the
next chat: Increment E is string interpolation — compile the already-parsed
interpolation parts (`StringTextPart`/`StringInterpolationPart`/
`StringExpressionPart`) to segment pushes and `BuildString`, and enforce the
nullable-interpolation compile error (D-279, E0102) using the `T?` resolution
this increment provides.
