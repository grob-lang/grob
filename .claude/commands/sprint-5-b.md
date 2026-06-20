---
description: "Sprint 5 · Increment B — named and default arguments. Default parameter values, the D-113 positional-then-named calling convention, and the four named-argument call-site diagnostics registered at E0008–E0011 (D-318). Type-checker and compiler argument-binding over Increment A's call machinery. No lambdas, no closures."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 5 · Increment B — named and default arguments

Increment A built positional calls over the call frame. This increment completes
the call surface: **default parameter values** and the **named-argument calling
convention** (D-113 — positional arguments first, named arguments after, only
parameters with defaults may be named). It also registers the four call-site
diagnostics D-113 specifies but the registry did not itemise, at the dedicated
codes assigned by **D-318**. This is type-checker and compiler argument-binding
work over A's call machinery — no new opcodes, no VM dispatch change.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 5** scope (the named-
   parameters bullet and its four compile-error cases), §3.5 (test routing).
2. `docs/design/grob-language-fundamentals.md` — the parameter-default and named-
   argument rules. Grep for the calling-convention rule (positional-then-named,
   only-defaulted-params-may-be-named) and the default-expression evaluation rule.
3. `docs/design/grob-error-codes.md` — confirm the **next-free** numbers in the
   **E00xx** Type block are **E0008**–**E0011** (E0006 undefined-method and E0007
   invalid-implicit-conversion are taken) before registering. Register the four
   codes with title, category, status, description and source decision **D-318**.
4. Decisions: **D-113** (named parameter calling convention) and **D-318** (the
   dedicated-codes decision). Grep the log; do not take their content from this
   prompt.

> **Verify before relying on cited decisions and sections.** Grep
> `grob-language-fundamentals.md` for the calling-convention and default rules,
> and `grob-error-codes.md` to confirm E0008–E0011 are genuinely the next-free
> slots. If the next-free number has moved because another increment landed a
> code, take the real next-free and surface the discrepancy with this prompt.
>
> **Named/default-argument rules — inline reference (authoritative source is
> `grob-language-fundamentals.md` and D-113; reproduced here so the
> implementation does not depend on a fetch landing well).**
>
> - **Default parameter values.** A parameter may declare a default:
>   `fn f(a: int, b: int = 10): int`. A call omitting a defaulted parameter binds
>   the default. Confirm the default-expression evaluation rule in the spec
>   (where and when the default evaluates) and match it. A parameter with no
>   default is **required**.
> - **Calling convention (D-113).** Positional arguments come first, in
>   declaration order. Named arguments come after, unordered relative to each
>   other. **Only parameters with defaults may be named** — required parameters
>   are positional-only.
> - **The four call-site diagnostics — dedicated codes (D-318).**
>   - **E0008** — a named argument appears **before** a positional argument.
>   - **E0009** — a named argument **names a required** (defaultless) parameter.
>   - **E0010** — a **duplicate** named argument (the same parameter named twice,
>     or named and also supplied positionally).
>   - **E0011** — a named argument names an **unknown** parameter.
>   These are distinct from **E0003** (wrong number of arguments) and **E0004**
>   (argument type mismatch), which still apply once binding succeeds. Each is
>   raised through its `ErrorCatalog` descriptor; no `"Exxxx"` literal at a call
>   site.
> - **Binding order in the checker.** Resolve positionals against the leading
>   parameters in order, then bind named arguments to the remaining defaulted
>   parameters, then fill any still-unbound defaulted parameters with their
>   defaults. Arity (E0003) and type (E0004) checks run on the **bound** argument
>   set. The four E0008–E0011 cases are detected during binding, before arity.
> - **Compiler emission.** The call site emits arguments in **parameter
>   declaration order** regardless of the source order of named arguments — the
>   callee reads slot 0 = first parameter, so the compiler reorders named
>   arguments and materialises omitted defaults into the correct positions. The
>   `Call` opcode and its arg-count operand are unchanged from Increment A; only
>   the argument-emission shape changes. No VM dispatch change.
> - **No new opcodes, no VM change.** The VM sees a fully-bound positional
>   argument list exactly as in Increment A. Named/default handling is entirely
>   front-end.
>
> **Sequencing note.** This is Increment B: A (call frames) → **B (named/default
> arguments)** → C (lambdas + natives) → D (closures) → E (init machine +
> narrowing) → F (close). Do not pull lambdas, natives, upvalue capture, the init
> machine or narrowing forward.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered plan — the checker's binding pass
   (defaults, named binding, the four E0008–E0011 cases, arity/type on the bound
   set), the compiler's reorder-and-fill emission, the registry and `ErrorCatalog`
   additions for the four codes, and the tests — and wait for Chris's approval.
2. On approval, run `/start-branch` and propose `feat/named-arguments`. Wait for
   Chris to create the branch.
3. Implement with TDD — tests written and confirmed **failing** first (follow
   `tdd-cycle`). For the four new codes, follow the `writing-an-error-test` skill:
   register the code, write the minimal `*_grob.txt` triggering exactly that case,
   generate `*_expected.txt` from the implementation, wire it into the negative-
   test gate.
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–4** as in Increment A's record, plus **Sprint 5 Increment A**:
  positional function declarations, calls and returns over the `CallFrame[256]`
  array, locals on slots over the frame base, recursion, the E5901 stack-overflow
  guard, `BytecodeFunction`, and positional arity/argument/return checking
  (E0003/E0004/E0005). All green.

## Deliverable for this increment

Default parameter values and the named-argument calling convention across the type
checker and the compiler, plus the four registered diagnostics. **No lambdas, no
natives, no upvalues.**

1. **Registry + catalog — four codes.** Register **E0008**/**E0009**/**E0010**/
   **E0011** in `grob-error-codes.md` (Type category, next-free in E00xx, source
   decision **D-318**) and add their `ErrorCatalog` descriptors (D-308). Confirm
   the next-free number against the registry first.
2. **Type checker — defaults and binding.** Bind positionals in order, then named
   arguments to defaulted parameters, then fill remaining defaulted parameters
   with their defaults. Detect E0008 (named before positional), E0009 (naming a
   required parameter), E0010 (duplicate), E0011 (unknown) during binding. Run
   E0003 arity and E0004 type checks on the bound set. Every identifier node
   carries `ResolvedType` and `Declaration`; the §3.1.1 invariant holds.
3. **Compiler — reorder and fill.** Emit arguments in parameter declaration order:
   reorder named arguments into their parameter positions and materialise omitted
   defaults into the correct slots, so the `Call` (unchanged) hands the callee a
   fully-bound positional list. The default expression compiles at the call site
   per the spec's evaluation rule.
4. **Diagnostics via `ErrorCatalog` (D-308).** The four new codes and the reused
   E0003/E0004 all raise through descriptors; no literals, no invented codes.

No new opcodes, no VM dispatch change, no parser or AST edits.

## Out of scope

Lambdas, `NativeFunction`, `RegisterNative`, the array higher-order methods
(Increment C). Upvalue capture (Increment D). The init state machine and narrowing
(Increment E). Do not edit the parser, the AST, the `OpCode` enum or the VM
dispatch loop — named/default handling is entirely front-end. If a diagnostic
seems to need a code beyond E0008–E0011 and not already registered, stop and
surface; do not invent.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - A call omitting a defaulted parameter binds the default; a call supplying it
    overrides the default.
  - Each of the four cases is its assigned code with a source location: named
    before positional (**E0008**), naming a required parameter (**E0009**),
    duplicate named argument (**E0010**), unknown parameter name (**E0011**).
  - A valid mix of positional and named arguments disassembles to arguments in
    **parameter declaration order**, with omitted defaults materialised in the
    correct positions (verified through the disassembler, not only the VM's
    answer).
  - E0003 and E0004 still fire correctly on the bound argument set.
  - §3.1.1 invariant holds for every identifier node introduced.
- **Error-example pairs (negative-test gate):** a `*_grob.txt` / `*_expected.txt`
  pair for each of E0008–E0011, generated from the implementation per
  `writing-an-error-test`.
- **VM tests (`Grob.Vm.Tests`):**
  - A hand-built chunk for a call that mixes a positional argument, a named
    argument and an omitted default returns the correct value — confirming the
    compiler's reorder-and-fill produced the right positional list.
- **Integration tests (`Grob.Integration.Tests`):**
  - A small script calling a function with a default omitted, a default overridden
    by name, and a named boolean argument (`recursive: true`-style) runs end-to-end
    and produces the expected stdout.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- Defaults bind and override correctly; positional-then-named calling works; the
  four call-site cases are their assigned codes with source locations; E0003/E0004
  still fire on the bound set.
- E0008–E0011 are registered in `grob-error-codes.md` and the catalog at the
  next-free numbers, citing D-318; the consistency gate (D-316) and the catalog
  agreement test (D-308) stay green.
- The §3.1.1 invariant holds; no new opcodes; no VM change; the DAG holds.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. The calling convention is settled by D-113 and the
codes are pre-assigned by D-318 — this is binding logic and registration over a
specified convention, not judgement. No Opus carve-out.

## Hand-off

Summarise: the binding order as built (positionals → named → defaults), the
reorder-and-fill emission shape, the four codes registered and their gold-master
pairs, and the test files added. Note for the next chat: Increment C is lambdas
and natives — the three lambda forms, the block-body implicit-last-expression
(D-276), `NativeFunction` + `RegisterNative`, the native↔VM call-back bridge, and
the `filter`/`select`/`sort`/`each` array higher-order methods as the lambda-
consuming natives, with top-level reference resolution only (D-296 categories 1–3,
**no upvalue capture**). C also makes the mechanical §6 `map` → `select`
correction.
