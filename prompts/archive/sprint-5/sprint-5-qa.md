# Grob — Sprint 5 External QA Brief

> **For:** GPT-5.3-Codex via Codex CLI (or an equivalent agentic model with
> terminal access to the repository)
> **Role:** Independent cold-reader and adversarial tester of the Grob Sprint 5
> (Functions and Closures) implementation, **as merged — inclusive of the
> post-close interlude**
> **From:** Chris — Lead Developer, sole author of Grob
> **Purpose:** A second pair of eyes, from a different model family, that has
> never seen the design corpus. You are here to find divergence between what the
> code does and what the spec says it must do. You are _not_ here to redesign the
> language.

You are running in the repository as an agent with a terminal — check out the
merged Sprint 5 branch, read the tree, build, test, write your own adversarial
fixtures and run them. You have the real working tree, not a snapshot; use it.

**The scope is the merged tree, not the lettered increments alone.** Sprint 5
shipped as increments **A–F** (functions, named/default args, lambdas, closures,
the init state machine, the close-gate) and then took an **in-sprint correctness
run plus a four-increment numbered interlude before Sprint 6**. Everything below
is in the branch you check out and is in scope:

- **In-sprint correctness increments:** **D-320** (`select` becomes a reserved
  identifier, not a hard keyword; new code **E1103**), **D-321** (top-level `fn`
  bindings runtime-hoisted, pass-1 registers value bindings, **E5902** narrowed),
  **D-322** (runtime diagnostics are `file:line` only — no column).
- **Numbered interlude (Increments 1–4):** **D-323** (three-pass type checker — new
  phase 1.5; new code **E0303**), **D-324** (**E1102** broadened to all top-level
  binding forms and retitled), **D-325** (upvalue closing is location-based, not
  value-based), **D-326** (function types `fn(...): T` become a first-class
  type-reference form).

Two of these falsify invariants the earlier draft of this brief asserted — there
are now **parser/AST edits** (D-320, D-326) and the checker is **three-pass**, not
two. Treat the bullets above as authoritative over any older framing, and the repo
as authoritative over both.

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
  dedicated named-argument codes). The merged interlude adds **D-320** (`select`
  reserved identifier; E1103), **D-321** (top-level `fn` hoisting; pass-1 value-
  binding registration; E5902 narrowed), **D-322** (runtime diagnostics are line-
  only), **D-323** (three-pass type checker, phase 1.5; E0303), **D-324** (E1102
  broadened and retitled), **D-325** (location-based upvalue closing) and **D-326**
  (function types as a first-class type reference). Look them all up.
- **`docs/design/grob-v1-requirements.md`** — the sprint plan and Definition of
  Done. **§6 (Sprint 5 — Functions and Closures)** is the scope contract: what
  ships and what does not. §3.1.1 is the day-one LSP invariant the type checker
  populates for every identifier node. §3.3 is the (closed) `OpCode` enum. §3.5
  routes tests. The §6 acceptance block is the bar. Note the acceptance was
  corrected this sprint from "filter, **map**, sort" to "filter, **select**, sort"
  (Increment C) — `map` no longer exists (D-280).
- **`docs/design/grob-language-fundamentals.md`** — the live spec for the
  constructs. The function-declaration grammar (typed parameters, explicit return
  type required), the parameter-default and named-argument rules, the **three-pass
  type checker** (D-166 established two passes; D-323 added phase 1.5 — registration
  → value-binding type resolution → body validation; pass 1 registers top-level
  declarations), the three lambda forms and the block-body rule (D-276), **§9** (the
  type-reference grammar, now including the function-type form `fn(...): T`, D-326),
  **§12.1** (the four-category variable resolution), **§17 / §17.2** (the three-pass
  model and value-binding type cycles — E0303, D-323), **§19.1** (the top-level
  initialisation state machine, refined by D-321) and the flow-sensitive narrowing
  rule (`if (x != nil)` narrows `T?` to `T`).
- **`docs/design/grob-vm-architecture.md`** — the call frame (`CallFrame`, the
  fixed `CallFrame[256]`, stack base, the `Call`/`Return` mechanics, locals as
  slots over the frame base), the upvalue mechanism (open vs closed, the
  close-on-return copy to the heap, the `Closure` object), native dispatch and the
  native↔VM call-back bridge, and the globals table the §19.1 state machine tags.
- **`docs/design/grob-error-codes.md`** — the registry (**total 109 codes**).
  Sprint 5 **registers** E0008–E0011 (the four named-argument call-site cases,
  D-318), **E1103** (reserved identifier used as a binding name — `select` and
  `formatAs`, D-320) and **E0303** (circular type dependency among top-level value
  bindings, compile-time, D-323); **broadens E1102** from `:=`-only to all top-level
  binding forms and retitles it "name already declared in this scope" (D-324); and
  **reuses** E0003 (wrong argument count), E0004 (argument type mismatch), E0005
  (return type mismatch), E0101 (nil dereference, outside narrowed scopes), E5901
  (call-stack overflow) and E5902 (circular initialisation — now **narrowed** to
  genuine value-binding cycles, D-321/D-323). Numbering is ADR-0014; stability
  ADR-0017.
- **`docs/design/grob-type-registry.md`** — the `T[]` higher-order methods
  (`filter`, `select`, `sort`, `each`) the lambdas flow through, and **`FunctionType`**
  (structural identity, invariant assignability, runtime-erased — D-326), the
  type-annotation form of a closure value.
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

**Then the merged interlude hardened it.** `select` became a reserved identifier so
`.select(...)` is grammatical from source (D-320, E1103); top-level `fn` bindings
are runtime-hoisted so a forward function call is no longer a circular-init trigger
(D-321), narrowing E5902 to genuine value-binding cycles; runtime diagnostics
settled at `file:line` with no column (D-322); the checker gained a value-binding
type-resolution phase (1.5) so forward value reads in function bodies see real
types, with unannotated value cycles caught at compile time as E0303 (D-323);
top-level redeclaration across every binding form is E1102 (D-324); upvalue closing
became location-based so closures escaping through a container close correctly
(D-325); and function types `fn(...): T` became a first-class type reference so a
named function can declare a closure return type (D-326). All of this is in the
merged branch and all of it is in scope.

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

### Merged interlude (test these too — they are in the branch)

- **`select` reservation and E1103 (D-320).** `select` lexes as `Identifier`, not a
  keyword; a leading `select (` at statement head is the `select` statement; an
  `expr.select(...)` is member access. `select` (and `formatAs`) used as a binding
  name — `select := 5` — is **E1103**. The six release-gate scripts that needed
  `.select(...)` (3, 5, 7, 8, 9, 12) now parse.
- **Top-level `fn` hoisting and the E5902 narrowing (D-321).** A top-level function
  called before its source position resolves and runs — it is **not** E5902. The
  compiler emits every top-level `fn`'s `DefineGlobal` in a prologue ahead of the
  first top-level statement. Pass 1 now registers top-level value bindings
  (`readonly`, mutable `:=`), not only `fn`/`type`.
- **Three-pass type checker and E0303 (D-323).** Phase 1.5 resolves top-level
  value-binding types in initialiser-dependency order before body validation, so a
  forward value read in a function body sees its real type (not `GrobType.Unknown`),
  closing the false-E0005 gap. An **unannotated** mutual value cycle is **E0303**
  (compile-time); an **annotated** mutual value cycle type-checks and surfaces as
  E5902 only at runtime.
- **Uniform top-level redeclaration, E1102 (D-324).** A name collision between any
  two top-level binding forms (`fn`, `type`, `readonly`, `const`, `:=`) is **E1102**,
  reported at the **second** declaration in source order — including the reverse-order
  value-before-`fn` case.
- **Location-based upvalue closing (D-325).** A closure that escapes its enclosing
  frame through a **container** (array element, map value — struct field is Sprint 6)
  closes its upvalue correctly. This is category-4 capture only; see §4.5.
- **Function types (D-326).** `fn(ParamTypes): ReturnType` is accepted by
  `ParseTypeRef` anywhere a type is written; see §4.11.
- **The §3.1.1 invariant on the new nodes.** A function-type-annotated identifier
  carries a `FunctionType` `ResolvedType`; a narrowed identifier carries `T`. All
  non-null per §4.1.

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
- **Escape through a container (D-325) — the interlude's headline fix.** Closing is
  **location-based, not value-based**: the VM keys open upvalues by stack slot and,
  on frame exit, closes every open upvalue at or above the returning frame's base
  into its heap cell, regardless of which heap object now references the closure.
  Drive the route that value-based closing missed — a closure returned **inside a
  container** and invoked after the frame popped: `return [inc]` then `arr[0]()`, and
  the map analogue (a closure stored as a map value, read out and called after
  return). The captured variable must read and write correctly with **no value-stack
  underflow**. An open upvalue left pointing at a truncated slot — a crash, a wrong
  read, or a stack underflow on `arr[0]()` — is a CORRECTNESS finding against D-325.
  (Struct-field escape is Sprint 6; do not test it. D-325 handles it by construction
  but the surface does not exist yet.)

### 4.6 The top-level initialisation state machine, E5902 and E0303 (§19.1, §17.2, D-294/D-321/D-323)

- A `GetGlobal` on a slot tagged `Uninitialised` **during startup** raises a
  `RuntimeError`; a read after the slot's `DefineGlobal` completes succeeds.
- **A forward `fn` call is no longer E5902 (D-321).** Top-level `fn` bindings are
  runtime-hoisted into a prologue, so a top-level statement that calls a function
  declared later in source runs cleanly. A finding "forward function call raises
  E5902" is now noise — and the inverse, a forward call that *does* raise E5902
  against the merged tree, is a CORRECTNESS finding against D-321.
- **The cycle split (D-323).** An **unannotated** mutual value cycle
  (`readonly a := b` / `readonly b := a`) is **E0303** at **compile time** (Type
  category), with both bindings assigned `GrobType.Error` for cascade suppression.
  An **annotated** mutual value cycle (`readonly a: int := b` / `readonly b: int := a`)
  type-checks — each type comes from its annotation, no back-edge forms — and
  surfaces as **E5902** at **runtime** when the still-`Uninitialised` value is read.
  A self-referential initialiser, direct or through a called function/lambda, is the
  runtime E5902 case. Verify both codes fire on the right form: an unannotated cycle
  reaching the VM (rather than E0303 at compile time) is a CORRECTNESS finding, as is
  an annotated cycle rejected at compile time (rather than running to E5902).
- `_startupComplete` short-circuits steady-state reads — a normal global read after
  startup must **not** pay the tag check. Verify the check is startup-scoped, not
  on every `GetGlobal` (a permanent tag check on every read is a SEMANTIC
  observation at most, but a tag check that wrongly fires after startup is a
  CORRECTNESS finding).
- `const` (D-296 category 1) is inlined and has no runtime slot — it is **not**
  tagged. A `const` being subject to the startup check is a finding.
- **Runtime diagnostics are line-only (D-322).** E5902 (and every runtime
  diagnostic) renders `file:line`, no column — the `Chunk`'s debug info is
  line-keyed. An expected-output fixture demanding a column on a runtime error is
  testing against a fabricated position; compile-time diagnostics (lexer/parser)
  still carry full `(file, line, column)` per §10/D-137.

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
- **The interlude codes are registered too.** `"E1103"` (reserved identifier used as
  a binding name, D-320) sits in the E11xx Name-resolution sub-block; `"E0303"`
  (circular type dependency among top-level value bindings, D-323) sits in the E03xx
  Type sub-block. Each appears **exactly once** as a descriptor. **E1102** was
  broadened and retitled to "name already declared in this scope" (D-324) — confirm
  the registry title matches the descriptor, not the old `:=`-only "variable already
  declared" wording. The canonical total is **109 codes**; confirm the footer total
  and the standing total line agree and that `Grob.Consistency.Tests` is green.
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

### 4.11 Function types as a first-class type reference (D-326)

`fn(ParamTypes): ReturnType` is accepted by `ParseTypeRef` anywhere a type is
written — a variable annotation, a parameter type, a `fn` return type. The point of
the change: a named function can now declare a closure return type, which v1's
"explicit return type on every function" rule otherwise made unwriteable. Verify by
fixture, type-check and disassemble:

- `makeCounter(): fn(): int { … }` type-checks — the previously rejected return type
  (E2001) is now expressible. A named function returning a closure that fails to
  type-check is a CORRECTNESS finding against D-326.
- **Suffix precedence:** `?` and `[]` bind to the **return type**. `fn(): int?` is a
  function returning `int?`; a nullable function value needs parens — `(fn(): int)?`
  — and likewise `(fn(): int)[]` for an array of them. A parse that binds `?`/`[]` to
  the whole function type without parens is a CORRECTNESS finding.
- **Structural identity, invariant assignability:** two function types match by
  structure (parameter types and return type), and assignability is **invariant** —
  no parameter/return variance in v1. A variance allowance is a CORRECTNESS finding;
  a function-type mismatch reuses the existing assignment/argument mismatch codes
  (no new code was minted — confirm none was).
- **Runtime-erased:** the callable is already a `GrobFunction`, so a function-type
  annotation produces **no** new opcode, no `.grobc` change and no `GrobValue`
  variant. A function-type annotation that perturbs the bytecode, the binary format
  or the value representation is a CORRECTNESS finding against the erasure guarantee.
- A function-type-annotated identifier carries a `FunctionType` `ResolvedType` after
  type-check (§4.1 invariant) — non-null, structural.

User-written function types are monomorphic; the registry's internal `→` generic
notation is unchanged and D-080's no-user-generics rule is untouched. A finding "I
cannot write a generic function type" misreads D-326/D-080.

### 4.12 `select` is a reserved identifier, not a keyword (D-320)

- The lexer emits `select` as `Identifier`. The statement parser promotes a leading
  `select (` at statement head to the `select` statement (D-301); `expr.select(...)`
  parses as member access (the D-280 universal transform). Both must parse from
  source — the bug D-320 fixed was that `.select(...)` was ungrammatical though the
  checker/VM/compiler supported it. A `.select(...)` that still fails to parse is a
  CORRECTNESS finding.
- `select` (and `formatAs`) used as a binding name is **E1103** (reserved identifier
  used as a binding name) — `select := 5` is E1103, not a successful declaration.
- The consistency gate (D-316) gained a native-method-name vs reserved-word check;
  confirm it is green. The v1 reserved set is `{ formatAs, select }`.

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
- **The interlude added parser/AST work — earlier "no parser/AST edits" framing is
  dead.** D-320 changed the lexer and statement parser (`select` reservation); D-326
  added the function-type form to `ParseTypeRef`. A finding "the AST/grammar was not
  supposed to change in Sprint 5" misreads the merged scope; the two named parser
  deltas are in scope and correct.
- **The checker is three-pass, not two (D-323).** Phase 1.5 (value-binding type
  resolution) sits between registration and body validation. A finding "there is an
  extra type-checker pass" misreads D-323; a phase-1.5 that resolves types in the
  wrong dependency order, or misses the unannotated-cycle E0303, is the finding.
- **A forward `fn` call is fine; it is not E5902 (D-321).** Top-level `fn` hoisting
  removed it. "Forward function call should raise E5902" is noise; a forward call
  that does raise E5902 is the finding.
- **Unannotated value cycle → E0303 (compile-time); annotated value cycle → E5902
  (runtime) (D-323).** Do not expect both on the same form. E0303 reaching the VM, or
  an annotated cycle rejected at compile time, is the finding.
- **Runtime diagnostics are line-only (D-322).** No column on runtime errors by
  design; compile-time diagnostics keep full position. A finding "the runtime error
  has no column" misreads D-322.

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
- **No new `src/` project and no new opcodes — but the merged tree _does_ carry
  parser/AST edits.** Increments A–F added no opcodes (the closed enum predates them)
  and no new project, and over A–F alone there were no parser/AST edits. The
  interlude then added two: `select` reservation in the lexer/statement parser
  (D-320) and the function-type form in `ParseTypeRef` (D-326). Those two are in
  scope and correct; a third, unexplained grammar change would be the finding.
- **`select` is a reserved identifier (D-320), not a keyword;** `select := x` is
  E1103, `.select(...)` is member access, a leading `select (` is the statement.
- **A forward `fn` call runs and is not E5902 (D-321);** top-level functions are
  hoisted. Only a value-binding cycle is a circular-init error.
- **An unannotated value cycle is compile-time E0303; an annotated one is runtime
  E5902 (D-323).** The three-pass checker (phase 1.5) is deliberate, not an extra
  pass to flag.
- **Runtime diagnostics are `file:line`, no column (D-322).** Compile-time
  diagnostics keep full position.
- **A closure escaping through a container closes correctly (D-325);** location-based
  closing is the design. A struct-field route is absent only because structs are
  Sprint 6.
- **Function types `fn(...): T` are first-class type references (D-326);** structural,
  invariant, runtime-erased. `(fn(): int)?` and `(fn(): int)[]` need parens — that is
  the suffix-precedence rule, not a parser bug.
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
   stdout); one fixture per diagnostic — E0008–E0011, **E1103** (`select := 5`) and
   **E0303** (unannotated value cycle) at compile time; E0003/E0004/E0005;
   E5901 and **E5902 (annotated value cycle only)** at runtime — expecting the
   diagnostic, the right exit and, for the compile-time ones, no execution;
   `calculator.grob` and `hello.grob` (Sprint 4 and Sprint 3 gates, expect no
   regression).
4. Compile-and-disassemble fixtures per §6 for every bytecode-shape claim in §4:
   the call/`Return` shape, the named/default reorder-and-fill, the lambda
   `BytecodeFunction`, the `Closure` upvalue descriptors and `GetUpvalue`/
   `SetUpvalue` indices. Confirm a function-type **annotation** emits no extra
   opcode (§4.11 erasure).
5. Drive the closure lifecycle through the VM with assertions (§4.5): open-upvalue
   sharing, close-on-return, per-call independence of two `makeCounter` closures,
   and — the interlude headline — a closure **escaping through a container**
   (`return [inc]` then `arr[0]()`, plus the map analogue) with no value-stack
   underflow.
6. Drive the init state machine through the VM (§4.6): an uninitialised-read during
   startup; a **forward `fn` call running cleanly** (not E5902, D-321); an
   **unannotated** value cycle caught at compile time as **E0303**; an **annotated**
   value cycle reaching runtime **E5902**; and a steady-state read after
   `_startupComplete` skipping the check. Confirm runtime diagnostics render
   `file:line` with no column (D-322).
7. Run the VM-execution benchmark category and confirm its baseline came from the
   workflow and passed the two-axis gate (§4.10).
8. Type-check function-type fixtures (§4.11): `makeCounter(): fn(): int` resolves;
   `(fn(): int)?` and `(fn(): int)[]` parse and the unparenthesised suffix binds to
   the return type; structural match and invariant assignability hold; no new error
   code was minted for function-type mismatches.
9. Read the type checker and compiler cold against the function, lambda, **§9**
   (function types), **§12.1**, **§17/§17.2** (three-pass, E0303) and **§19.1**
   sections of the fundamentals. Write your own adversarial inputs per §4 — prefer
   adding throwaway xUnit tests in the existing projects (routed per §3.5) over
   scratch harnesses, so assertions are reproducible. Note that `FsCheck` is not in
   the project (plain xUnit `[Theory]` rows are the idiom) — match it.

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
