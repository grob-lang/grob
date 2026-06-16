---
name: "Sprint 3 · Increment E — String Interpolation"
description: "Compile the already-parsed interpolation parts to segment pushes and BuildString in Grob.Compiler, and enforce the nullable-interpolation compile error (D-279, E0102). Uses the T? resolution from Increment D."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 3 · Increment E — String Interpolation

You are continuing Sprint 3. Increment D delivered the nullable runtime. This
increment compiles string interpolation: the parser already produces the
interpolation parts, so the work is type-checking the slots and emitting segment
pushes plus the `BuildString` opcode. The one type-system rule is the
nullable-interpolation compile error (D-279), which uses the `T?` resolution
Increment D provides.

Read, in order:

1. `docs/design/grob-v1-requirements.md` §4 (Sprint 3 — the
   `InterpolatedString`/`BuildString` bullet), §3.3 (`BuildString` — "concatenate
   N string fragments", 1-byte count), §3.5 (test routing).
2. `docs/design/grob-language-fundamentals.md` — the string-literal section
   (§8): `${expr}` interpolation, the escape set (`\n \r \t \\ \" \$`),
   `\$` for a literal `$`, no line spanning; and the **nullable-interpolation**
   rule.
3. `docs/design/grob-type-registry.md` — the `.toString()` / display surface for
   how a non-string interpolated value becomes a string fragment, if the spec
   specifies an implicit conversion at interpolation slots.
4. `docs/design/grob-error-codes.md` — E0102 (nullable interpolation), E2009
   (unterminated string interpolation — a lexer/syntax error, already handled).
5. Decision **D-279** (nullable interpolation is a compile error) and **D-308**
   (`ErrorCatalog`) in the log.

> **Verify before relying on cited decisions and node names.** Grep the log to
> confirm D-279 (interpolating a `T?` is a compile error, resolved by `??` or —
> in Sprint 5 — narrowing). **Confirm the container interpolated-string
> expression node's name in `Grob.Compiler/Ast/Expressions` before relying on
> it** — the on-disk parts are `StringTextPart`, `StringInterpolationPart` and
> `StringExpressionPart`; §4 calls the feature `InterpolatedString`, which is the
> spec name, not necessarily the class name. Use the actual class names from the
> tree.
>
> **Interpolation compilation — inline reference (authoritative source is §8;
> reproduced so the implementation does not depend on a fetch landing well).**
> - A double-quoted interpolated string is a sequence of parts: literal text
>   fragments and `${expr}` slots. Compile each part in order: push the literal
>   text fragment as a string constant; compile each `${expr}` to its value and
>   convert it to a string fragment per the registry's display/`toString` rule.
> - After all N fragments are on the stack, emit `BuildString` with the count N
>   (1-byte operand); it concatenates the N fragments into one string.
> - Escapes are already resolved by the lexer (`\n \r \t \\ \" \$`); `\$`
>   produced a literal `$` and did not open a slot; unknown `\x` was a lexer
>   error. This increment does not re-handle escapes.
>
> **Nullable interpolation — inline reference (D-279, E0102).** Interpolating an
> expression whose static type is nullable (`T?`) is **E0102**. This applies to
> direct nullable bindings, functions returning `T?`, optional chains
> (`${user?.name}` — the chain result is `T?`) and try-parse results. The
> resolution in Sprint 3 is `?? <fallback>` (`${user?.name ?? "guest"}` is
> valid). Narrowing-based resolution (`if (x != nil) { … "${x}" … }`) is Sprint
> 5 — do not implement narrowing here; in Sprint 3 a nullable slot is E0102
> unless a `??` has resolved it to non-nullable.
>
> **Sequencing note.** This is Increment E of the agreed breakdown: A → B → C →
> D → **E (interpolation)** → F. It depends on D for the `T?` resolution behind
> E0102. Do not pull narrowing forward.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name `feat/string-interpolation`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprint 1 & 2:** front end and back end complete; the parser produces the
  interpolation parts (`StringTextPart`/`StringInterpolationPart`/
  `StringExpressionPart` — confirm the container node name). `BuildString` is in
  the closed `OpCode` enum. `ErrorCatalog` (D-308), C# 14 (D-310).
- **Sprint 3 A–D:** declarations and assignment with scope/slots; `grob run`;
  `const`/`readonly`; the nullable runtime — `T?` rules, `??`, `?.`, `IsNil`,
  and the `emitJump`/`patchJump` helper. The `T?` resolution behind E0102 is
  available from D.

## Deliverable for this increment

String-interpolation compilation in the type checker and compiler
(`Grob.Compiler`). **No parser work — the parts already exist.**

1. **Type checker — interpolation slots.** Resolve each `${expr}`'s type. A slot
   whose static type is nullable (`T?`) is **E0102** (use D's `T?` resolution; a
   `${a ?? b}` slot resolves to non-nullable and is valid). A non-string,
   non-nullable slot converts to string per the registry's display/`toString`
   rule.
2. **Compiler — segment emission.** Emit each part in source order: literal text
   fragments as string constants, `${expr}` slots as their compiled value
   converted to a string fragment. Then emit `BuildString` with the fragment
   count N.
3. **Diagnostics via `ErrorCatalog`.** E0102 raises through its catalog
   descriptor. Do not invent codes.

## Out of scope

No narrowing-based resolution of nullable slots (Sprint 5) — `??` is the only
Sprint 3 resolution. No new escape handling — the lexer resolved escapes in
Sprint 1. No `const`-RHS interpolation support — `const` rejected interpolated
strings as non-constant in Increment C (E0205); that rejection stands and is not
revisited here. Do not edit the parser, AST or `OpCode` enum. Do not touch
`Grob.Vm` beyond confirming `BuildString` runs — its arm exists from Sprint 2 if
the enum was wired then; if `BuildString` has no VM arm yet, add only that arm
and surface that you did.

## Tests (`Grob.Compiler.Tests` unless noted)

- `"a${x}b"` compiles to: push "a", compile `x` → string fragment, push "b",
  `BuildString 3`. Assert the fragment order and the count.
- A string with no slots compiles to a single string constant (no `BuildString`,
  or `BuildString 1` — decide and document which, matching the spec).
- `"${n}"` where `n: int` converts via the display rule; assert the result.
- `"${user?.name}"` is E0102 (chain result is `T?`); `"${user?.name ?? "g"}"`
  is valid.
- `"\$5.00"` is the literal text `$5.00`, not a slot (lexer-resolved; assert the
  compiled constant).
- **Integration (`Grob.Integration.Tests`):** a script building an interpolated
  message from declared variables runs end-to-end via `grob run` and prints the
  expected string, including a `${nullable ?? "fallback"}` slot.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- Interpolated strings compile to ordered fragment pushes plus `BuildString N`
  and execute to the correct string.
- A nullable slot is E0102; a `??`-resolved slot is valid.
- E0102 raises via its catalog descriptor.
- The §3.1.1 invariant holds; the DAG, parser, AST and `OpCode` enum are
  unchanged.

## Model

Sonnet 4.6 (High) throughout — segment emission and the E0102 check are
transcription against the inline reference and D's `T?` resolution. No Opus
call-out; there is no structural decision later sprints build on here.

## Hand-off

Summarise: the container node name as found, the segment-emission sequence, the
display/`toString` conversion at slots, the no-slot string handling, the E0102
enforcement using D's `T?` resolution, whether a `BuildString` VM arm had to be
added, and the test files added. Note for the next chat: Increment F is the REPL
and sprint close — `grob repl` (`G>` prompt, auto-printed expression results,
multi-line input, history), the end-to-end `grob run hello.grob` smoke script
that closes the sprint, and the first VM-execution benchmark baseline via the
`benchmark.yml` workflow (D-309).
