# Grob — Sprint 5 External QA Brief

> **For:** GPT-5.3-Codex via Codex CLI (or an equivalent agentic model with
> terminal access to the repository)
> **Role:** Independent cold-reader and adversarial tester of the Grob Sprint 5
> (Functions and Closures) implementation
> **From:** Chris — Lead Developer, sole author of Grob
> **Purpose:** A second pair of eyes, from a different model family, that has
> never seen the design corpus. You are here to find divergence between what the
> code does and what the spec says it must do. You are _not_ here to redesign the
> language.

You are running in the repository as an agent with a terminal — check out the
merged Sprint 5 branch, read the tree, build, test, write your own adversarial
fixtures and run them. You have the real working tree, not a snapshot; use it.

---

## 0. Read the spec from the repository — do not trust this brief for spec detail

This brief is a navigation aid, not a source of truth. It tells you _where to
look_ and _what to test_; it deliberately does not reproduce the specification,
because a paraphrase drifts and you would then test against the drift. **The
authority is the repository.** Where this brief and a repo document disagree, the
repo document wins; flag the disagreement.

Before testing anything, locate and read these (verify the paths against the real
tree first — a missing or relocated spec file is itself a finding):

- **`docs/design/grob-decisions-log.md`** — the authority on every design
  decision. Numbered `D-###`. When anything looks wrong, search here before
  filing it; most surprises are deliberate and have an entry. This file outranks
  every other document and outranks this brief. Sprint 5 leans on **D-113**
  (named-parameter calling convention), **D-115** (lambdas and closures),
  **D-276** (block-body lambda return), **D-280** (`select` replaced `map`),
  **D-281** (`sort` key-selector), **D-294** (top-level initialisation state
  machine), **D-296** (four-category closure resolution), **D-303** (`GrobValue`),
  **D-308** (`ErrorCatalog`), **D-310** (C# 14 / .NET 10), **D-313** (two-axis
  benchmark gate), **D-314** (the Claude Code harness) and **D-318** (the
  dedicated named-argument codes). Look them up.
- **`docs/design/grob-v1-requirements.md`** — the sprint plan and Definition of
  Done. **§6 (Sprint 5 — Functions and Closures)** is the scope contract: what
  ships and what does not. §3.1.1 is the day-one LSP invariant the type checker
  populates for every identifier node. §3.3 is the (closed) `OpCode` enum. §3.5
  routes tests. The §6 acceptance block is the bar. Note the acceptance was
  corrected this sprint from "filter, **map**, sort" to "filter, **select**, sort"
  (Increment C) — `map` no longer exists (D-280).
- **`docs/design/grob-language-fundamentals.md`** — the live spec for the
  constructs. The function-declaration grammar (typed parameters, explicit return
  type required), the parameter-default and named-argument rules, the two-pass
  type checker (D-166 — pass 1 registers top-level declarations), the three lambda
  forms and the block-body rule (D-276), **§12.1** (the four-category variable
  resolution), **§19.1** (the top-level initialisation state machine) and the
  flow-sensitive narrowing rule (`if (x != nil)` narrows `T?` to `T`).
- **`docs/design/grob-vm-architecture.md`** — the call frame (`CallFrame`, the
  fixed `CallFrame[256]`, stack base, the `Call`/`Return` mechanics, locals as
  slots over the frame base), the upvalue mechanism (open vs closed, the
  close-on-return copy to the heap, the `Closure` object), native dispatch and the
  native↔VM call-back bridge, and the globals table the §19.1 state machine tags.
- **`docs/design/grob-error-codes.md`** — the registry. Sprint 5 **registers**
  E0008–E0011 (the four named-argument call-site cases, D-318) and **reuses**
  E0003 (wrong argument count), E0004 (argument type mismatch), E0005 (return type
  mismatch), E0101 (nil dereference, outside narrowed scopes), E5901 (call-stack
  overflow) and E5902 (circular initialisation). Numbering is ADR-0014; stability
  ADR-0017.
- **`docs/design/grob-type-registry.md`** — the `T[]` higher-order methods
  (`filter`, `select`, `sort`, `each`) the lambdas flow through, and the function
  types.
- **`docs/design/grob-benchmarking-strategy.md`** — §8/§9: the VM-execution
  baseline production path (`benchmark.yml`, D-309) and the two-axis gate
  (`Grob.BenchCheck`, D-313).

Published wiki renderings may exist under `docs/wiki/`; the design docs are the
working corpus and the decisions log is the authority. If they disagree, prefer
the design docs and flag the drift.

---

## 1. What Sprint 5 delivers, in one paragraph

Sprint 4 made the language branch and loop. Sprint 5 makes it **call, return and
close over**, across six increments over the type checker, the compiler and the VM
opcode arms — **no new `src/` project and no new opcodes** (the function and
closure opcodes — `Call`, `Return`, `Closure`, `GetUpvalue`, `SetUpvalue`,
`CloseUpvalue` — were all defined when the enum closed in Sprint 2 A, and this
sprint writes their compiler emission and VM dispatch arms for the first time):
**A** — `fn` declarations, the `CallFrame[256]` array, `Call`/`Return`, locals as
slots over the frame base, recursion and the **E5901** stack-overflow guard, with
positional arity/argument/return checking (E0003/E0004/E0005); **B** — named and
default arguments under the D-113 positional-then-named convention, with the four
call-site cases at **E0008** (named before positional) / **E0009** (naming a
required parameter) / **E0010** (duplicate) / **E0011** (unknown), registered
citing **D-318**; **C** — the three lambda forms (block-body implicit-last-
expression with `return` early exit, D-276), `NativeFunction`/`RegisterNative`,
the native↔VM call-back bridge, and the `filter`/`select`/`sort`/`each` array
higher-order methods, with **top-level reference resolution only** (D-296
categories 1–3, no capture); **D** — closures: category-4 upvalue capture
(`Closure`/`GetUpvalue`/`SetUpvalue`/`CloseUpvalue`), open upvalues over live
stack slots, closed upvalues copied to the heap on the enclosing function's
return, the `Closure` object, independent capture per call; **E** — the top-level
initialisation state machine (§19.1, D-294 — the `SlotState` tag, the
`DefineGlobal`/`GetGlobal` transitions, `_startupComplete`, **E5902** on circular
initialisation) and flow-sensitive narrowing (`if (x != nil)` narrows `T?` to
`T`); **F** — the `functions.grob` close-gate (Sprint 1–5 surface, no stdlib
modules), the §6 acceptance and the third VM-execution benchmark baseline. The
acceptance bar is the §6 block: functions call and return; recursion works; named
and default arguments work with the four cases caught; lambdas work in `filter`,
`select` and `sort`; closures capture with per-call independence; the init machine
catches circular initialisation; narrowing narrows `T?` to `T`; the type checker
catches arity and argument-type mismatches; `grob run functions.grob` runs. User
types, structs and `#{ }` are Sprint 6.

---

## 2. The single most important instruction: report, do not patch

This codebase reflects deliberate decisions backed by the decisions log. Many
things that look like bugs are settled. **Your default action is to file a
finding, not to change code.** Before filing anything as a defect, search
`grob-decisions-log.md` for a relevant `D-###`.

Two buckets, every finding in exactly one:

- **CORRECTNESS** — the code does not do what a cited spec section says it must.
  A `Return` that leaves the wrong stack depth, a recursion that crashes the host
  instead of raising E5901, a default argument bound to the wrong parameter, a
  named-before-positional call that is not E0008, an upvalue that reads a dead slot
  after the enclosing function returned, two `makeCounter` closures that share a
  counter, a `GetGlobal` during startup that does not consult the `SlotState` tag,
  a narrowed `x` still typed `T?` inside `if (x != nil)`, a `ResolvedType`/
  `Declaration` null after type-check, an `"E0008"` literal at a call site instead
  of a catalog descriptor. You may propose a patch, clearly labelled as a proposal.
- **SEMANTIC / DESIGN** — you are questioning _what the language does_ rather than
  _whether the code matches spec_. **Report only, never patch, one line each.**
  Assume it is deliberate and a decision-log entry exists. If you can cite the
  `D-###` that settles it, it is not a finding at all.

If unsure which bucket, it is SEMANTIC / DESIGN.

---

## 3. Scope — what is in Sprint 5 and what is not

The authoritative scope is §6 of `grob-v1-requirements.md`. Read it. The summary
below orients; the document governs.

### In scope (test these)

- **Call frames and return (A).** `fn` declarations with typed parameters and an
  explicit return type; `Call`/`Return` over the `CallFrame[256]` array; locals as
  slots over `frame.StackBase`; recursion; forward references resolving (pass-1
  registration, D-166); positional arity (E0003), argument type (E0004) and return
  type (E0005); the **E5901** call-stack-overflow guard raising a `RuntimeError`,
  never a host `StackOverflowException`.
- **Named and default arguments (B).** Default parameter values; the D-113
  positional-then-named convention; **only defaulted parameters may be named**; the
  four call-site cases E0008 (named before positional), E0009 (naming a required
  parameter), E0010 (duplicate), E0011 (unknown parameter); the compiler emitting
  arguments in **parameter declaration order** with omitted defaults materialised.
- **Lambdas and natives (C).** The three lambda forms; the block-body implicit
  last-expression with `return` as a **lambda-local** early exit (D-276); lambdas
  as first-class `Function` values; D-296 **categories 1–3** resolution (const
  inline, global read/write); `NativeFunction`/`RegisterNative`; transparent
  native dispatch through the `Call` arm; the **native↔VM call-back bridge**
  (re-entrant); `filter`/`select`/`sort`/`each` on `T[]` (new arrays, no mutation;
  `sort` stable, key-selector D-281).
- **Closures (D).** Category-4 upvalue capture; `Closure`/`GetUpvalue`/
  `SetUpvalue`/`CloseUpvalue`; open upvalues over live slots; closed upvalues on
  the enclosing function's return; the `Closure` object (`BytecodeFunction` +
  upvalue array); transitive capture; **independent capture per enclosing-function
  call**.
- **Init state machine + narrowing (E).** The §19.1 `SlotState`
  `Uninitialised`/`Initialising`/`Initialised` tag; `DefineGlobal`/`GetGlobal`
  transitions; the startup-only check and `_startupComplete` short-circuit;
  **E5902** on circular initialisation; flow-sensitive narrowing of `T?` to `T`
  inside `if (x != nil)`, removed after the block.
- **Close (F).** `grob run functions.grob` over the **Sprint 1–5 surface, no
  stdlib modules** (recursion, named/default args, a `makeCounter` closure, a
  `filter`/`select`/`sort` pipeline with a capturing lambda, an `if (x != nil)`
  narrowing); the third VM-execution benchmark baseline via the workflow against
  the two-axis gate.
- **The §3.1.1 invariant, extended.** Every identifier node introduced across A–F
  carries a non-null `ResolvedType` and a non-null `Declaration` after type-check
  (`UnresolvedDecl.Instance` at error paths, D-311; `GrobType.Error` on the type
  side).
- **The Sprint 1–4 surface, lightly** — Sprint 5 must not have regressed it.
  `grob run calculator.grob` (Sprint 4's gate) and `grob run hello.grob` (Sprint
  3's) still produce their expected stdout; control flow unchanged.

### Explicitly OUT of scope (do not test, do not flag as missing)

- **User-defined types, structs, the `type` keyword, `#{ }`** — **Sprint 6.** A
  lambda body or the close-gate using a struct is a finding (it tests a later
  sprint early). `select(f => SomeType { … })` projections are Sprint 6; the
  Sprint 5 `select` returns scalar/array element types.
- **Stdlib modules** (`fs`, `json`, `csv`, `process`, `date`, `regex`, `path`,
  `formatAs`, …) — **Sprint 8.** The `filter`/`select`/`sort`/`each` array methods
  are **built-in type methods implemented as natives**, not stdlib modules; a
  finding "`filter` should be in `grob-stdlib-reference.md`" misreads the split.
  `print`/`input`/`exit` are built-ins (resolved at type-check), not stdlib.
- **A `Call`/`Return`/`Closure`/upvalue opcode being "added".** These opcodes
  pre-existed in the **closed** enum (Sprint 2 A); Sprint 5 wires their emission
  and dispatch. "A `Call` opcode should be added to the enum" misreads §3.3 — the
  enum is complete.
- **Generic function or type declaration.** You **consume** generics in v1; you
  cannot **declare** them (D-080). `filter`/`select`/`sort` having generic
  signatures you cannot author yourself is correct.
- **Multiple return values, top-level `return`, operator overloading.** Not v1
  features; their absence is not a finding.
- **Narrowing on forms other than `if (x != nil)`** unless §6 / the fundamentals
  lists them. A finding "narrowing does not apply to `switch`/`==`/assignment" is
  noise unless the spec specifies that form.
- **The full benchmark suite / stability calibration** — Sprint 8. Sprint 5 adds
  the third VM-execution baseline only; "the regression gate is not calibrated" is
  out of scope.
- **Per-error-code long-form docs** (`docs/errors/Exxxx.md`). Scheduled before v1;
  absence is not a Sprint 5 finding.

A finding of the form "there are no user-defined types" or "the stdlib has no
`fs`" is noise. Discard it.

---

## 4. The Sprint 5 invariants — highest-value targets

These are the non-deferrable acceptance criteria and the structural pieces later
sprints build on. Confirm the exact wording in the spec before asserting. Prefer a
written assertion or a test — and for bytecode shape, a compile-and-disassemble —
over an eyeball read for every one.

### 4.1 The §3.1.1 invariant holds across every new identifier node

Walk a parsed-and-type-checked AST for a source file using every Sprint 5 form —
function declarations and calls, named/default arguments, the three lambda forms,
capturing closures, the narrowed identifier inside `if (x != nil)` — and assert
**every identifier node carries a non-null `ResolvedType` and a non-null
`Declaration`**. Per D-311 the declaration side uses the `UnresolvedDecl.Instance`
singleton at error paths, not literal `null`; assert it by reference
(`Assert.Same`). A narrowed identifier must carry `T` (not `T?`) inside the block
and `T?` after — both non-null. A genuinely null/unset `Declaration` or
`ResolvedType` is a CORRECTNESS finding.

### 4.2 Call frames, the stack base, recursion and the E5901 guard

The call frame is the foundation B–E build on. Verify empirically, by
compile-and-disassemble and by VM execution:

- A call disassembles to callee + arguments + `Call` with the correct arg-count
  operand; the callee's chunk ends in `Return`.
- `Return` resets `stackTop` to discard the callee's locals and the callee value,
  leaving exactly the return value — assert the stack height before and after.
- Locals resolve against `frame.StackBase` — a function's slot 0 is its first
  parameter, independent of the caller's stack depth.
- Direct and mutual recursion compute correctly (factorial; an even/odd pair).
- A non-terminating recursion raises **E5901** as a `RuntimeError` with a source
  line and **does not** crash the host with a CLR `StackOverflowException`. This is
  the highest-value runtime check in A — a host crash here is a CORRECTNESS finding.

### 4.3 Named and default binding, and the four call-site codes

- A call omitting a defaulted parameter binds the default; supplying it overrides.
  The default materialises in the **correct slot** — verify by disassembly that a
  mixed positional/named call emits arguments in **parameter declaration order**.
- Each of the four cases is its assigned code with a source location: named before
  positional (**E0008**), naming a required parameter (**E0009**), duplicate named
  argument or named-and-positional for the same parameter (**E0010**), unknown
  parameter name (**E0011**). A case landing on E0003 instead of its dedicated code
  is a CORRECTNESS finding against D-318.
- E0003 (arity) and E0004 (argument type) still fire on the **bound** argument set.

### 4.4 Lambdas, the block-body return, categories 1–3, and the native bridge

- Each lambda form type-checks and is a `Function` value; a block-body lambda
  returns its implicit last expression, and a `return` inside it exits the
  **lambda only**, not an enclosing function (D-276). A `return` in a block-body
  lambda that exits the enclosing function is a CORRECTNESS finding.
- A lambda referencing a top-level `const` compiles to a constant-pool load; a
  top-level `readonly`/mutable reference to a global read; a write to a top-level
  mutable to a global write — and **no** `Closure`/upvalue opcode is emitted for a
  top-level-only lambda (categories 1–3). An upvalue opcode on a non-capturing
  lambda is a CORRECTNESS finding (it pulls D's machinery into C).
- The VM dispatches `NativeFunction` and `BytecodeFunction` through the same `Call`
  arm transparently.
- **The native↔VM call-back bridge:** a native (`filter`/`select`/`sort`/`each`)
  invokes its lambda argument per element by re-entering the VM, and the
  frame/stack discipline holds across the boundary — assert the result and the
  post-call stack depth. `filter`/`select`/`sort` return **new** arrays and never
  mutate the source; `sort` is **stable** and key-selector only (D-281). A mutated
  source array, or an unstable sort, is a CORRECTNESS finding.

### 4.5 Closures — open/closed upvalues, close-on-return, per-call independence

There is no shortcut here; the upvalue lifecycle only submits to empirical proof.

- A capturing lambda disassembles to `Closure` + `GetUpvalue`/`SetUpvalue` with the
  right upvalue descriptors; transitive capture (a lambda inside a lambda
  referencing the outer function's local) resolves to an upvalue-of-enclosing
  chain.
- **Open upvalues:** while the enclosing function is on the stack, a write through
  one closure's upvalue is seen by another closure capturing the **same** open
  slot, and by the enclosing function.
- **Close-on-return:** after the enclosing function returns, the closure still
  reads and writes the captured variable correctly (it was copied to the heap). A
  closure that reads a dead or reused slot after return is a CORRECTNESS finding.
- **Per-call independence:** two closures from two calls to the same enclosing
  function (`c1 := makeCounter(); c2 := makeCounter()`) hold **separate** state —
  `c1()` and `c2()` count independently. Shared state here is the headline
  CORRECTNESS finding of the sprint.

### 4.6 The top-level initialisation state machine and E5902 (§19.1, D-294)

- A `GetGlobal` on a slot tagged `Uninitialised` **during startup** raises a
  `RuntimeError`; a read after the slot's `DefineGlobal` completes succeeds.
- A circular initialisation — a top-level binding whose initialiser reads itself,
  directly or through a called function/lambda — raises **E5902** with a source
  line.
- `_startupComplete` short-circuits steady-state reads — a normal global read after
  startup must **not** pay the tag check. Verify the check is startup-scoped, not
  on every `GetGlobal` (a permanent tag check on every read is a SEMANTIC
  observation at most, but a tag check that wrongly fires after startup is a
  CORRECTNESS finding).
- `const` (D-296 category 1) is inlined and has no runtime slot — it is **not**
  tagged. A `const` being subject to the startup check is a finding.

### 4.7 Flow-sensitive narrowing

- Inside `if (x != nil) { }`, `x` has `ResolvedType` `T` (not `T?`); after the
  block, `x` is `T?` again. A dereference of the nullable inside the narrowed block
  type-checks; the same dereference outside the block is **E0101** (nil
  dereference). A body that does not narrow, or a narrowing that leaks past the
  block, is a CORRECTNESS finding against the narrowing rule.

### 4.8 `ErrorCatalog` parity and no invented codes (D-308)

- `"E0008"`–`"E0011"` each appear **exactly once** in the solution (their
  descriptors) and are registered in `grob-error-codes.md` at the next-free numbers
  in the E00xx block, citing **D-318**. A duplicated literal, an unregistered code
  or a code raised by a string literal at a call site is a CORRECTNESS finding.
- The reused codes — **E0003**, **E0004**, **E0005**, **E0101**, **E5901**,
  **E5902** — are raised through their existing descriptors, not re-minted. A
  Sprint 5 diagnostic raising one of these as a fresh literal is a finding.
- No invented codes: grep for `"E0`, `"E1`, `"E2`, `"E5` literals outside the
  catalog; any hit is a finding. Every code a Sprint 5 diagnostic raises exists in
  the registry. The consistency gate (D-316) asserts catalog↔registry agreement —
  confirm it is green.

### 4.9 The close-gate and the negative paths

- `grob run functions.grob` through the real CLI produces the expected stdout over
  the **Sprint 1–5 surface, with no stdlib modules**. A wrong answer is the
  highest-priority CORRECTNESS finding. Confirm the script stays within the surface
  — if it uses a struct, the `type` keyword or a stdlib module, that is itself a
  finding (the gate tests a later sprint early).
- The inverse, per new/used diagnostic class: named before positional (E0008),
  naming a required parameter (E0009), duplicate named argument (E0010), unknown
  parameter (E0011), an arity/type mismatch (E0003/E0004), a return-type mismatch
  (E0005), a deep recursion (E5901 at runtime) and a circular initialisation
  (E5902 at runtime) each produce their diagnostic with a line number; the
  compile-time ones do not execute, the runtime ones stop on first error with exit
  code 1.

### 4.10 The VM-execution benchmark baseline came from the workflow (D-309/D-313)

Sprint 5 lays the **third** VM-execution baseline. Confirm it was produced via the
`benchmark.yml` workflow on the canonical runner — not a local `dotnet run -c
Release` — and that `Grob.BenchCheck` ran the two-axis comparison (5% per-sprint
against the rolling baseline, 12% cumulative against the frozen origin, D-313). A
locally produced committed baseline, or one on a different runner type, is a
CORRECTNESS finding (it voids cross-run comparison). Gate calibration is out of
scope; baseline provenance is in.

---

## 5. The sprint-specific corrections to verify, not re-litigate

- **`select`, not `map` (D-280).** The §6 acceptance was corrected this sprint from
  "filter, map, sort" to "filter, select, sort"; `map` does not exist. A finding
  "there should be a `map` method" or "`select` should be called `map`" misreads
  D-280. If you think the rename is wrong, that is one SEMANTIC line.
- **The four named-argument codes E0008–E0011 are newly assigned this sprint
  (D-318); E0003/E0004/E0005/E5901/E5902 pre-existed.** The named-arg codes are new
  registry entries at the next-free numbers in the E00xx block (ADR-0014,
  ADR-0017) — "these codes did not exist before" is expected for E0008–E0011. The
  function/recursion/return/overflow/circular-init codes are **not** new — a
  finding framing E0003 or E5902 as newly created this sprint is noise. Verify all
  exist, are referenced via catalog descriptors, and carry the §-wording.
- **Lambdas resolve categories 1–3 in C; category-4 capture is D — one sprint, two
  increments.** Both must work in the **merged** Sprint 5 branch. A non-capturing
  lambda emitting an upvalue opcode, or a capturing lambda failing to, is a
  CORRECTNESS finding; "C does not implement capture" against the merged branch is
  noise (D does).
- **Only defaulted parameters may be named (D-113).** Required parameters are
  positional-only; naming one is **E0009** by design. A finding "I cannot name a
  required parameter" misreads D-113.
- **The array higher-order methods are built-in type methods, not stdlib modules.**
  Their absence from `grob-stdlib-reference.md` is correct; the stdlib modules are
  Sprint 8.

A "this `:=` could be a collection expression" or "this method could be a primary
constructor" suggestion is stylistic modernisation and out of scope. The build is
the detector for real C# 14 / .NET 10 defects (D-310): build clean in Debug and
Release and read the warning list.

---

## 6. The bisection tools still apply

The disassembler (Sprint 2 A) is the localisation tool for this sprint's bytecode
work. Where an end-to-end script gives a wrong answer, compile the fixture,
disassemble the `Chunk` and read it: the call's argument order and `Call`
arg-count operand, the `Return` shape, the named/default reorder-and-fill, the
lambda's `BytecodeFunction`, the `Closure` upvalue descriptors and the
`GetUpvalue`/`SetUpvalue` indices are all visible there. Prefer fixture-and-
disassemble over inspection for every bytecode-shape claim in §4 — the compiler is
where the bugs live, and the disassembler exists to make compiler-side faults
unambiguous.

For the closure lifecycle specifically, pair the disassembly with VM execution:
the open→closed hand-off and per-call independence (§4.5) do not show in the
listing alone — drive two closures through the VM and assert their state is
separate. A disassembler that prints upvalue or `Call` operands incorrectly is a
CORRECTNESS finding, because it defeats the bisection it exists for.

---

## 7. Things that may look wrong but are likely deliberate

Before filing any of these as CORRECTNESS, find the governing `D-###`. If the log
confirms the behaviour it is not a finding; if the code contradicts the log,
_that_ is the finding.

- **No new opcodes; the closed enum already carries `Call`/`Return`/`Closure`/
  `GetUpvalue`/`SetUpvalue`/`CloseUpvalue`** (Sprint 2 A). Sprint 5 wires their
  emission and dispatch. "An opcode should be added" misreads §3.3.
- **`select`, not `map` (D-280).** Not a finding.
- **The array natives are built-in type methods, not stdlib modules** (Sprint 8).
- **Only defaulted parameters may be named (D-113);** required parameters are
  positional-only, and naming one is E0009.
- **A `return` inside a block-body lambda exits the lambda, not the enclosing
  function (D-276).**
- **Top-level references (categories 1–3) are not capture (D-296);** only an
  enclosing-function local is captured as an upvalue. A non-capturing lambda
  correctly emits no upvalue opcode.
- **Per-call capture independence is the design;** each enclosing-function call has
  its own captured slot. Two `makeCounter` closures not sharing state is correct.
- **`_startupComplete` short-circuits the init-state check (§19.1);** steady-state
  global reads skip the tag check by design — that is not a "missing" check.
- **`const` is inlined and untagged (D-296 category 1).** It is not subject to the
  startup state machine.
- **Recursion is bounded by `CallFrame[256]`, raising E5901 — not the C# call
  stack.** A deep recursion raising E5901 rather than a host crash is correct.
- **No flow narrowing outside `if (x != nil)`** unless the spec lists the form.
  Sprint 5 narrows the `!= nil` guard; richer narrowing is not a v1 feature.
- **No new `src/` project, no new opcodes, no parser/AST edits.** Sprint 5 is
  type-checker, emission, VM-arm and native-registration work over Sprint 1's
  grammar-complete AST and Sprint 2's closed enum.
- **The harness is Claude Code (D-314);** the Opus carve-out for this sprint is the
  `grob-closure-specialist` subagent under `.claude/agents/`, methodology not
  language surface. Not a finding.

The rule throughout: the decisions log is the authority. "This language should do X
differently" is a SEMANTIC observation at most — one line, no patch — and if the
log settles it, it is nothing.

---

## 8. How to run the work

1. Check out the merged Sprint 5 branch. Build in Debug and Release
   (`dotnet build`, `dotnet build -c Release`); note warnings, prioritise only
   those indicating a real defect (the C# 14 / .NET 10 check, §5).
2. Run the test suite (`dotnet test`). Report any failure verbatim — a failing test
   in delivered code is a high-priority CORRECTNESS finding.
3. `grob run` against fixtures: the close-gate `functions.grob` (expect the §6
   stdout); one fixture per diagnostic (E0008–E0011 at compile time; E0003/E0004/
   E0005; E5901 and E5902 at runtime) expecting the diagnostic, the right exit and
   — for the compile-time ones — no execution; `calculator.grob` and `hello.grob`
   (Sprint 4 and Sprint 3 gates, expect no regression).
4. Compile-and-disassemble fixtures per §6 for every bytecode-shape claim in §4:
   the call/`Return` shape, the named/default reorder-and-fill, the lambda
   `BytecodeFunction`, the `Closure` upvalue descriptors and `GetUpvalue`/
   `SetUpvalue` indices.
5. Drive the closure lifecycle through the VM with assertions (§4.5): open-upvalue
   sharing, close-on-return, and — the headline — per-call independence of two
   `makeCounter` closures.
6. Drive the init state machine through the VM (§4.6): an uninitialised-read during
   startup, a circular initialisation raising E5902, and a steady-state read after
   `_startupComplete` skipping the check.
7. Run the VM-execution benchmark category and confirm its baseline came from the
   workflow and passed the two-axis gate (§4.10).
8. Read the type checker and compiler cold against the function, lambda, §12.1 and
   §19.1 sections of the fundamentals. Write your own adversarial inputs per §4 —
   prefer adding throwaway xUnit tests in the existing projects (routed per §3.5)
   over scratch harnesses, so assertions are reproducible. Note that `FsCheck` is
   not in the project (plain xUnit `[Theory]` rows are the idiom) — match it.

---

## 9. Output format

Produce a single findings document. No prose preamble and no summary of how Grob
works — that is in the repo. For each finding:

```text
[CORRECTNESS | SEMANTIC] — one-line title
  Increment: A | B | C | D | E | F | cross-cutting
  File:      path:line
  Spec:      document and section, or "none / unsure"
  Decision:  D-### if you found a governing entry, or "none found"
  Observed:  what the code does
  Expected:  what the spec requires (CORRECTNESS only)
  Repro:     minimal input or test that demonstrates it (CORRECTNESS only)
  Patch:     proposed diff, labelled as a proposal (CORRECTNESS only, optional)
```

Order: all CORRECTNESS first, by severity (crash / wrong-output / minor), then all
SEMANTIC as a flat list. If a section produced no findings, say so in one line —
"§4.5 closures: open/closed lifecycle and per-call independence verified correct,
no findings" is useful signal. Where you relied on a spec file that was missing or
differently located than §0 expected, say so at the top.

Do not fix anything in the SEMANTIC bucket. Do not redesign. Find where the code
lies to the spec — as the spec actually reads in the repo, not as this brief
paraphrases it — and tell me precisely where.
