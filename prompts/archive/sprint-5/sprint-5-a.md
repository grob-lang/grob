---
description: "Sprint 5 · Increment A — fn declarations, call frames, return. Call/Return wired into dispatch, CallFrame + the fixed CallFrame[256] array, stack base, locals as slots over the frame, recursion, the E5901 stack-overflow guard, BytecodeFunction. Positional-call arity and return-type checking. The structural increment B–F reuse."
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
model: sonnet
---

# Sprint 5 · Increment A — fn declarations, call frames, return

You are opening Sprint 5 of the Grob compiler — functions and closures. Sprint 4
made the language branch and loop. This increment makes it **call and return**:
`fn` declarations get call frames, the `Call` and `Return` opcodes get their
compiler emission and VM dispatch arms for the first time, locals live on stack
slots over a per-frame base, recursion falls out naturally, and the call-stack
overflow guard raises **E5901**. It is the structural foundation every later
Sprint 5 increment reuses — B's argument binding, C's lambda values, D's upvalues
and E's startup reads all sit on the call frame built here, so the frame and slot
discipline built here is load-bearing.

Read, in order:

1. `docs/design/grob-v1-requirements.md` — the **Sprint 5 — Functions and
   Closures** scope (the `fn` declaration, `Call`/`Return`, `CallFrame`,
   `CallFrame[256]`, recursion and `BytecodeFunction` bullets), §3.1.1 (the
   day-one LSP properties), §3.3 (the closed `OpCode` enum — confirm `Call` and
   `Return` are present and that no case needs adding), §3.5 (test routing).
2. `docs/design/grob-language-fundamentals.md` — the function declaration grammar
   (typed parameters, explicit return type required in v1), the two-pass
   type-checker rules (D-166 — pass 1 registers top-level declarations, so
   forward references to functions declared later resolve), and the
   no-top-level-`return` rule. Grep for the explicit-return-type rule and the
   forward-reference rule.
3. `docs/design/grob-vm-architecture.md` — **Call Frames and the Call Stack**: the
   `CallFrame` struct (function reference, instruction pointer, stack base), the
   `CallFrame[256]` array, the `Call` and `Return` handler sketches, locals as
   stack slots over `frame.StackBase`, and the stack-overflow guard.
4. Decisions: confirm the entries governing **functions / call frames** and
   **named parameters** (the calling convention D-113 — this increment builds the
   **positional** path only; named arguments are Increment B). Grep the log; do
   not take their content from this prompt.

> **Verify before relying on cited decisions and sections.** Grep
> `grob-language-fundamentals.md` to confirm the function-declaration and
> two-pass-checker rules read as this prompt assumes, and the decisions log for
> the call-frame and named-parameter entries. If a section has moved or a
> decision been superseded, surface it rather than proceeding.
>
> **Call-machinery rules — inline reference (authoritative source is
> `grob-vm-architecture.md` and `grob-language-fundamentals.md`; reproduced here
> so the implementation does not depend on a fetch landing well).**
>
> - **`fn` declaration.** `fn name(p: Type, ...): ReturnType { body }`. Typed
>   parameters and an explicit return type are required in v1. The two-pass type
>   checker (D-166) already registers every top-level `fn` in pass 1, so a call
>   may forward-reference a function declared later in the same file. This
>   increment validates the **body** (pass 2) and the **call sites**; it does not
>   re-build pass-1 registration. A function compiles to a `BytecodeFunction` —
>   its own `Chunk`, its arity and its parameter/return types.
> - **`Call` (1-byte arg count).** Push the callee and its arguments, then `Call`
>   pushes a `CallFrame { Function, InstructionPointer = 0, StackBase = stackTop -
>   argCount }` and dispatches into the callee's chunk. The arguments the caller
>   pushed become the callee's first locals automatically (slot 0 = first param).
>   Match the stack-base arithmetic in `grob-vm-architecture.md` exactly.
> - **`Return`.** Pop the return value, decrement the frame count, reset
>   `stackTop` to `frame.StackBase - 1` (discarding the callee's locals and the
>   callee value itself), push the return value. Execution resumes in the caller's
>   chunk at the caller's saved instruction pointer.
> - **Locals as slots over the frame.** Local variables live at
>   `_stack[frame.StackBase + slot]` — the same slot machinery Sprint 3 A built
>   for block scoping, now based at the frame rather than at a single global base.
>   `GetLocal`/`SetLocal` decode against the active frame's base. No dictionary, no
>   string lookup.
> - **Recursion.** Falls out of the call frame with no special handling — a
>   function that calls itself pushes another frame. The only bound is the frame
>   array.
> - **Stack overflow (E5901).** When the frame count would exceed the fixed
>   `CallFrame[256]` capacity, raise the `RuntimeError` **E5901** (call-stack
>   overflow) through its `ErrorCatalog` descriptor, carrying the source line. The
>   VM stops on the first runtime error. A deep recursion must raise E5901, never
>   crash the host with a CLR `StackOverflowException` — the frame count is
>   checked, the C# call stack is not the bound.
> - **Arity and return-type checking.** A positional call supplying the wrong
>   number of arguments is **E0003** (wrong number of arguments). An argument
>   whose type does not match the parameter is **E0004** (argument type mismatch).
>   A `return` whose value type does not match the declared return type is
>   **E0005** (return type mismatch). All three codes already exist in
>   `grob-error-codes.md` — reuse them through their `ErrorCatalog` descriptors;
>   do not register new codes. Named arguments and defaults are Increment B; this
>   increment checks the positional path.
> - **No new opcodes.** `Call` and `Return` already exist in the closed enum
>   (Sprint 2 A). This increment writes their compiler emission and VM dispatch
>   arms — it does not add an enum case. If you reach to add one, stop and
>   surface.
>
> **Sequencing note.** This is Increment A of the agreed Sprint 5 breakdown:
> **A (call frames)** → B (named/default arguments) → C (lambdas + natives) →
> D (closures) → E (init machine + narrowing) → F (close). Do not pull named
> arguments, defaults, lambdas, upvalue capture, the init state machine or
> narrowing forward — those are their own increments. This increment is the
> positional call machinery only.

## Branching, planning and commits

This work does **not** go on `main`.

1. Work in **plan mode**. Present a numbered implementation plan — the
   type-checker arms (pass-2 body validation, positional arity/return checking),
   the compiler emission for `fn` bodies and `Call`/`Return`, the VM dispatch arms
   for `Call`/`Return` and the frame array, and the tests — and wait for Chris's
   approval before editing. The plan is the gate.
2. On approval, run `/start-branch` and propose `feat/functions-and-call-frames`.
   Wait for Chris to create the branch.
3. Implement with TDD — tests written and confirmed **failing** first. Follow the
   `tdd-cycle` skill. The `Call`/`Return` opcode arms follow the `adding-an-opcode`
   skill's emit-and-dispatch-together discipline even though the enum case already
   exists (the enum step is a no-op; the compiler-emission and VM-dispatch steps
   are not).
4. Run `/commit-message` against the staged changes — it refuses on `main`.
5. Stop after the local commit. Push and PR are Chris's.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprints 1–2:** the front end (lexer, grammar-complete AST including `fn`
  declaration, lambda and call-expression nodes, the error-recovering parser) and
  the back end (closed `OpCode` enum **including `Call`, `Return`, `Closure`,
  `GetUpvalue`, `SetUpvalue`, `CloseUpvalue` defined but not yet emitted**,
  `GrobValue` (D-303) with the `Function` variant, `Chunk`, disassembler, VM
  dispatch loop, two-pass type checker, compiler, BenchmarkDotNet skeleton). C# 14
  / .NET 10 (D-310), `ErrorCatalog` (D-308), the consistency gate (D-316). All
  green.
- **Sprint 3:** variables, the scope chain, locals on slots, `PopN`, `grob run`,
  `const`/`readonly`, the nullable runtime, string interpolation, the REPL, the
  first benchmark baseline.
- **Sprint 4:** control flow — `if`/`else if`/`else`, `&&`/`||`, ternary, `while`,
  `break`/`continue`, `for...in`, `select`, the switch expression — and the
  `calculator.grob` close. All green, QA and QA-fix landed. The 4→5 interlude
  (D-315/D-316/D-317) is merged.

## Deliverable for this increment

Positional function declarations, calls and returns across the type checker, the
compiler and the VM. The parser and AST are stable — every node exists.
**No named arguments, no defaults, no lambdas, no upvalues, no init machine, no
narrowing.**

1. **Type checker — function bodies (pass 2).** Validate every `fn` body against
   its declared return type. A `return` value whose type does not match the
   declared return type is **E0005** with a source location. Set `ResolvedType`
   and `Declaration` on every identifier node in the body; the §3.1.1 invariant
   holds. (Pass 1's top-level registration already exists — D-166 — reuse it.)
2. **Type checker — positional call sites.** Resolve the callee, check the
   argument count against the parameter count (**E0003**) and each argument type
   against its parameter type (**E0004**), and annotate the call expression with
   the function's return type. Confirm the exact codes against the registry; do
   not invent.
3. **Compiler — function bodies.** Compile each `fn` to a `BytecodeFunction` with
   its own `Chunk`. Parameters occupy the first local slots; `Return` is emitted
   for explicit returns and for the end of a non-void body per the spec's return
   rule.
4. **Compiler — calls.** Emit the callee, the argument expressions in order, then
   `Call` with the argument count.
5. **VM — `Call` / `Return` dispatch.** Implement the two handlers against the
   `CallFrame[256]` array and the stack-base arithmetic in
   `grob-vm-architecture.md`. `GetLocal`/`SetLocal` resolve against the active
   frame's base. The **E5901** stack-overflow guard fires when the frame count
   would exceed capacity — a `RuntimeError`, not a host crash.
6. **Diagnostics via `ErrorCatalog` (D-308).** E0003, E0004, E0005 and E5901 all
   raise through their existing catalog descriptors. No `"Exxxx"` literal at a
   call site; no invented codes.

No new opcodes (the enum is closed), no parser or AST edits.

## Out of scope

Named arguments and the named-argument diagnostics (Increment B). Default
parameter values (Increment B). Lambdas, `NativeFunction`, `RegisterNative` and
the array higher-order methods (Increment C). Upvalue capture and the `Closure`
object (Increment D). The top-level initialisation state machine and flow-
sensitive narrowing (Increment E). All-paths-return analysis is a spec question —
confirm whether the fundamentals require it in v1; if the spec is silent, surface
it rather than implementing a check with an invented code. Do not edit the parser,
the AST or the `OpCode` enum. If you believe you need an opcode that is not there,
stop and surface.

## Tests

Per §3.5, route each test to the project matching its kind.

- **Type-checker / compiler tests (`Grob.Compiler.Tests`):**
  - A positional call with too few / too many arguments is **E0003** with a source
    location; a mismatched argument type is **E0004**; a `return` whose value type
    differs from the declared return type is **E0005**.
  - A forward call to a function declared later in the file resolves (pass-1
    registration, D-166).
  - A `fn` body disassembles to its own `Chunk` with parameters in the first slots
    and a `Return` at the end; a call disassembles to callee + args + `Call`
    (arg-count operand verified through the disassembler, not only the VM's
    answer).
  - §3.1.1 invariant: every identifier node in a type-checked function body and
    call carries a non-null `ResolvedType` and a non-null `Declaration` (sentinels
    by reference, `Assert.Same`).
  - Layer-invariant `[Theory]` rows: pathological but parseable call/declaration
    inputs type-check to a result or a diagnostic, never throw.
- **VM tests (`Grob.Vm.Tests`):**
  - A hand-built chunk for a call returns the correct value and leaves the stack at
    the expected depth (the callee's locals and the callee value are discarded on
    `Return`).
  - A directly recursive function (e.g. factorial) computes correctly.
  - A non-terminating recursion raises **E5901** as a `RuntimeError` and does
    **not** crash the host with a CLR `StackOverflowException`.
- **Integration tests (`Grob.Integration.Tests`):**
  - A small script declaring and calling a function (including one recursive call)
    runs end-to-end via `grob run` and produces the expected stdout.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- Functions declare, call and return; recursion works; positional arity, argument-
  type and return-type mismatches are compile errors with source locations; a deep
  recursion raises E5901 rather than crashing the host.
- The §3.1.1 invariant holds for every identifier node introduced.
- No new opcodes, no parser/AST edits; the DAG holds (`Grob.Compiler` and
  `Grob.Vm` never reference each other).
- Diagnostics raise through `ErrorCatalog` descriptors; no literals, no invented
  codes.
- Coverage at or above 90% on the affected projects.

## Model

Sonnet 4.6 (High) throughout. The call-frame mechanics are settled by
`grob-vm-architecture.md` and the codes already exist — this is transcription over
a specified machine, not judgement. There is no Opus carve-out in this increment.

## Hand-off

Summarise: the `CallFrame` shape and stack-base arithmetic as built, how locals
resolve against the frame base, how recursion and the E5901 guard behave, the
positional arity/return checks and their catalog codes, and the test files added.
Note for the next chat: Increment B is named and default arguments — the D-113
positional-then-named binding and the four call-site diagnostics at **E0008**
(named before positional) / **E0009** (naming a required parameter) / **E0010**
(duplicate named argument) / **E0011** (unknown parameter name), registered in B
citing **D-318**.
