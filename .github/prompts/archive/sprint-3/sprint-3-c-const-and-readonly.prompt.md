---
name: "Sprint 3 · Increment C — `const` and `readonly`"
description: "Build const and readonly in Grob.Compiler — the shared SingleAssignmentDeclaration node, the compile-time-constant evaluator over the §24 allowlist with reference inlining and no runtime slot, the readonly deep-immutability checker, the cross-reference rules. Assigns E0205/E0206 to the registry and ErrorCatalog."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 3 · Increment C — `const` and `readonly`

You are continuing Sprint 3. Increment A delivered mutable declarations, the
scope chain and the globals/locals/slot machinery; Increment B surfaced `grob
run`. This increment adds the two single-assignment binding forms: `const`
(compile-time constant, evaluated by the type checker and inlined by the
compiler) and `readonly` (runtime-once, evaluated at the declaration point and
never mutated). They share one AST node and one diagnostic surface but are
different machines underneath — a compile-time-constant evaluator versus a
deep-immutability checker — which is why they land together.

Read, in order:

1. `docs/design/grob-v1-requirements.md` §4 (Sprint 3 — the `const`/`readonly`/
   `SingleAssignmentDeclaration` bullets) and §3.5 (test-project routing).
2. `docs/design/grob-language-fundamentals.md` **§24** (`const` and `readonly`
   semantics) — read it in full; it is the spec for this increment, including
   the compile-time-constant allowlist, the stdlib constant whitelist, the
   `readonly` deep-immutability rules, `param`-implicitly-`readonly` and the
   cross-reference rules.
3. `docs/design/grob-type-registry.md` — how `const`-inlined values reach the
   constant pool, and `GrobType` for the bindings' resolved types.
4. `docs/design/grob-error-codes.md` — the existing `const`/`readonly` codes
   (E0201–E0204) and the numbering scheme (ADR-0014); you add E0205 and E0206.
5. `docs/design/grob-solution-architecture.md` — `Grob.Compiler` responsibilities
   and the DAG.
6. Decisions **D-288** (split into `const`/`readonly`), **D-289** (definition of
   compile-time-constant expression), **D-291** (`readonly` semantics, deep
   immutability, `param` implicitly `readonly`), **D-308** (`ErrorCatalog`) in
   `docs/design/grob-decisions-log.md`.

> **Verify before relying on cited decisions.** Grep the decisions log to
> confirm D-288, D-289 and D-291 say what this prompt assumes (D-288 = the
> `const`/`readonly` split; D-289 = the compile-time-constant allowlist; D-291 =
> `readonly` runtime-once with deep immutability and `param` implicitly
> `readonly`). If any has been superseded or renumbered, surface it.
>
> **`const` compile-time-constant allowlist — inline reference (authoritative
> source is §24 / D-289; reproduced so the implementation does not depend on a
> fetch landing well).**
>
> **Allowed on a `const` right-hand side:**
> - Literals of `int`, `float`, `string`, `bool`, `nil` — all literal forms
>   (decimal/hex/binary/underscored ints; floats; raw backtick strings;
>   double-quoted strings **without** `${...}`; `true`/`false`/`nil`).
> - Binary arithmetic, comparison and logical operators on compile-time-constant
>   operands: `+ - * / % == != < <= > >= && ||`.
> - Unary `-` and `!` on compile-time-constant operands.
> - String concatenation via `+` on two compile-time-constant strings.
> - References to other `const` identifiers declared earlier in the file.
> - References to named stdlib constants on the whitelist:
>   `math.pi`, `math.e`, `math.tau`; `path.separator`, `path.altSeparator`,
>   `path.pathSeparator`, `path.lineEnding`; `guid.empty`,
>   `guid.namespaces.{dns,url,oid,x500}`. (A stdlib symbol qualifies only if it
>   is a named primitive value with no runtime cost; functions never qualify.)
>
> **Not allowed (each is E0205 — RHS is not a compile-time constant):**
> - Function calls of any kind, including stdlib calls like `math.min(1, 2)` —
>   the rule is mechanical, not purity analysis; even calls that could in
>   principle be folded are rejected.
> - Struct construction, array/map/anonymous-struct literals.
> - Any call into `env.* date.* fs.* process.* http.*` or any plugin.
> - Interpolated strings — any `${...}` makes the string non-constant even when
>   every slot is itself `const`. Compose with `+`.
> - Lambdas, optional chaining (`?.`), nil coalescing (`??`), ternary (`? :`).
> - References to `readonly` or mutable identifiers (the `readonly` reference is
>   E0206 specifically — see below).
>
> Float compile-time evaluation uses the host .NET IEEE 754 semantics (stable
> across identical .NET versions).
>
> **`readonly` rules — inline reference.**
> - Any valid expression is legal on the right-hand side. It is evaluated at the
>   declaration point (top-level: source order, per §19.1; inside a body: at
>   execution).
> - Compiles to the **same** `DefineGlobal`/`DefineLocal` path as a mutable
>   binding — `readonly` occupies a normal slot/global. Immutability is a
>   compile-time check, not a runtime tag.
> - Cannot be reassigned (E0202) and cannot be mutated (E0204). Mutation
>   includes `arr.append(x)`, `arr[i] = v`, `map[k] = v`, `point.field = v`
>   where the bound value is `readonly`, and `++`/`--`/`+=` on a `readonly int`.
>   This is **deep**: any operation that would change the bound value is
>   rejected.
> - Must be initialised at declaration — no deferred initialisation.
> - `param` bindings are implicitly `readonly` (E0203 on reassignment); the
>   keyword is not written on them.
>
> **Cross-reference rules — inline reference.**
> - A `readonly` may reference any `const` on its RHS (`const` values are
>   resolved before any `readonly` is evaluated).
> - A `const` may **not** reference a `readonly` identifier on its RHS — the
>   `readonly` value does not exist at compile time. This is **E0206**.
>
> **Sequencing note.** This is Increment C of the agreed breakdown: A → B →
> **C (`const`/`readonly`)** → D → E → F. Do not pull nullable handling or
> interpolation forward — but note that the `const` allowlist already *excludes*
> `??`, `?.` and interpolation, so this increment must reject them as non-
> constant even though their full semantics arrive in D and E. Rejecting a form
> as non-constant is not the same as implementing it.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name `feat/const-and-readonly`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprint 1 & 2:** front end and back end complete; the AST is grammar-complete
  — confirm whether the `SingleAssignmentDeclaration` node (with its
  `Const`/`Readonly` `Kind` discriminator) already exists in
  `Grob.Compiler/Ast/Declarations`; the parser is grammar-complete, so it likely
  does. If it does, this increment type-checks and compiles it; if it does not,
  surface that before assuming parser work is needed. `ErrorCatalog` (D-308),
  C# 14 (D-310), `UnresolvedDecl.Instance` (D-311).
- **Sprint 3 A:** mutable declarations, assignment, scope chain, globals, locals
  on slots, `PopN`, type annotations, assignment sugar.
- **Sprint 3 B:** `grob run <file>`.

## Deliverable for this increment

`const` and `readonly` in the type checker and compiler (`Grob.Compiler`), plus
the two new error codes. **No nullable, no interpolation, no parser work.**

1. **Type checker — `const`.** In pass 2, evaluate the right-hand side as a
   compile-time constant per the allowlist above. A form outside the allowlist
   is **E0205**. A reference to a `readonly` identifier is **E0206**. Store the
   evaluated value; resolve the binding's type from it; register the binding so
   later references can be inlined.
2. **Compiler — `const` inlining.** Intern the evaluated value into the constant
   pool and emit a `Constant` load at **every** reference site. A `const` has
   **no runtime slot** — there is no `DefineGlobal`/`DefineLocal` for it.
   Reassignment (E0201) and mutation are compile errors; `++`/`--` on a `const`
   is E0201.
3. **Type checker — `readonly`.** Validate the right-hand side as any expression.
   Tag the binding immutable. Reject reassignment (E0202) and deep mutation
   (E0204) at compile time, per the deep-immutability rule above. `param`
   bindings carry the same immutability (reassignment is E0203).
4. **Compiler — `readonly`.** Emit the same `DefineGlobal`/`DefineLocal` path as
   a mutable binding — `readonly` occupies a normal slot/global; only the
   compile-time checks differ.
5. **Cross-reference rules.** `readonly`-references-`const` is fine;
   `const`-references-`readonly` is E0206. Enforce in pass 2.
6. **The immutability representation.** Whatever carries "this binding is
   immutable, reject reassignment and deep mutation" must be shaped so that
   Sprint 4's immutable iterator variables and Sprint 5's parameters extend it
   rather than reinvent it. See the model note.
7. **Error codes E0205 and E0206 (D-308 / ADR-0014).** Add both to
   `grob-error-codes.md` in the `const`/`readonly` block at the next free
   numbers (E0205 = "right-hand side of `const` is not a compile-time constant
   expression"; E0206 = "`const` binding cannot reference a runtime/`readonly`
   value"), and add their descriptors to the `ErrorCatalog`. The `"E0205"` and
   `"E0206"` strings appear exactly once each — in their catalog descriptors.
   Use the §24 error-message wording. Update `grob-error-codes.md`'s count/index
   if it tracks one.

## Out of scope

No nullable types, `??`, `?.`, `IsNil`, `NilCoalesce` (Increment D) — but reject
them as non-constant on a `const` RHS per the allowlist. No string-interpolation
compilation (Increment E) — but reject interpolated strings on a `const` RHS. No
control flow (Sprint 4), functions/closures (Sprint 5), structs (Sprint 6) — the
deep-mutation rule names struct-field and array-index mutation, so reject those
forms when the target is `readonly`, but do not implement struct or collection
*semantics* here. Do not edit the parser, AST or `OpCode` enum (closed). Do not
touch `Grob.Vm` — `const`/`readonly` are entirely a `Grob.Compiler` concern
(inlining and compile-time checks); the VM sees only the `Constant`/`DefineX`
opcodes it already runs.

## Tests (`Grob.Compiler.Tests` unless noted)

- `const MAX := 3` then a reference emits a `Constant` load, not a global read;
  no `DefineGlobal` is emitted for the `const`.
- `const TAU := math.pi * 2` evaluates at compile time; `const G := "Hi " + NAME`
  (NAME a `const`) concatenates at compile time.
- `const X := env.require("K")` is E0205; `const X := math.min(1, 2)` is E0205
  (function call); `const X := "Hi ${NAME}"` is E0205 (interpolation).
- `const C := R` where `R` is `readonly` is E0206; `readonly R := C` where `C`
  is `const` is valid.
- `const MAX := 3; MAX = 4` is E0201; `MAX++` is E0201.
- `readonly T := env.get("K")` accepts the runtime RHS and emits a normal
  `DefineGlobal`; `T = "x"` is E0202; for a `readonly` array, `ITEMS.append(4)`
  and `ITEMS[0] = 9` are E0204; `++` on a `readonly int` is E0204.
- A `param` reassignment is E0203.
- §3.1.1 invariant holds across a `const`/`readonly` tree — every identifier
  non-null `ResolvedType` and `Declaration` (sentinel by reference at error
  paths).
- **`ErrorCatalog` parity:** assert `"E0205"`/`"E0206"` exist exactly once
  (their descriptors) and that the diagnostics raise via the descriptors, not
  literals — extend the existing catalog/registry parity test if one is present.
- **Integration (`Grob.Integration.Tests`):** a script using a local `const`
  for a magic number and a `readonly` for a once-computed value runs end-to-end
  via `grob run` and prints the expected result.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `const` rejects non-compile-time RHS (E0205), `readonly` references (E0206),
  reassignment (E0201) and mutation; references inline as constant-pool loads
  with no runtime slot.
- `readonly` accepts any runtime RHS, occupies a normal slot/global and rejects
  reassignment (E0202) and deep mutation (E0204); `param` reassignment is E0203.
- The cross-reference rules hold both ways.
- E0205 and E0206 are registered in `grob-error-codes.md` and the
  `ErrorCatalog`, each literal appearing exactly once.
- The §3.1.1 invariant holds; the DAG, parser, AST, `OpCode` enum and `Grob.Vm`
  are unchanged.

## Model

Sonnet 4.6 (High) throughout. The compile-time-constant evaluator is
transcription against the allowlist inlined above — walk the RHS, fold the
admitted forms, reject the rest with E0205 — so it stays on Sonnet. The one
structural candidate is the **immutability representation**: `param` is already
implicitly `readonly`, Sprint 4 adds immutable iterator variables and Sprint 5
adds parameters, so the mechanism that says "reject reassignment and deep
mutation of this binding" wants to be shaped once so later sprints extend rather
than duplicate it. With §24's deep-immutability rules inlined above in front of
it, Sonnet handles it; reach for Opus only if deep-mutation detection across
containers, struct fields and indexed targets needs a judgement call. The
trigger is "later sprints extend this shape", not "this part is hard".

## Hand-off

Summarise: the `SingleAssignmentDeclaration` handling (and whether the node
pre-existed), the compile-time-constant evaluator's structure and the allowlist
forms it folds versus rejects, the `const` inlining path (constant pool, no
slot), the `readonly` deep-immutability representation and how it is shaped for
Sprint 4/5 reuse, the cross-reference enforcement, the E0205/E0206 registry and
catalog additions, and the test files added. Note for the next chat: Increment D
is the nullable runtime — `T?` type rules, `??` (`NilCoalesce`, eager per the
spec as written), `?.` optional-chaining short-circuit, `IsNil`. D owns
first-use of forward-jump backpatching for the `?.` short-circuit, and builds
the `emitJump`/`patchJump` helper Sprint 4's control flow reuses.
