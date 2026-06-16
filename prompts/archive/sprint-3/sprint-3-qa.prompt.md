# Grob — Sprint 3 External QA Brief

> **For:** GPT-5.3-Codex (or equivalent agentic model with terminal access)
> **Role:** Independent cold-reader and adversarial tester of the Grob Sprint 3 implementation
> **From:** Chris — Lead Developer, sole author of Grob
> **Purpose:** A second pair of eyes that has never seen the design corpus. You are
> here to find divergence between what the code does and what the spec says it must do.
> You are _not_ here to redesign the language.

---

## 0. Read the spec from the repository — do not trust this brief for spec detail

This brief is a navigation aid, not a source of truth. It tells you _where to look_
and _what to test_, but it deliberately does not reproduce the specification text,
because a paraphrase drifts from the real document and you would then test against the
drift. **The authority is the repository.** Where this brief and a repo document
disagree, the repo document wins; flag the disagreement so it can be fixed.

Before testing anything, locate and read these files. The design corpus lives under
`docs/design/` (project history — present in the repo, not published to the wiki).
The filenames retain the `grob-` prefix. Verify the paths against the real tree before
relying on them:

- **`docs/design/grob-decisions-log.md`** — the authority on every design decision.
  Numbered `D-###` entries. When anything looks wrong, search this file before filing
  it: most surprises are deliberate and have an entry here. This file outranks every
  other document and outranks this brief. Sprint 3 leans on D-137, D-166, D-288,
  D-289, D-291, D-279, D-303, D-308, D-309, D-310, D-311 — and possibly a new entry
  governing `??` evaluation order (see §5). Look them up; do not take their content
  from this brief.
- **`docs/design/grob-v1-requirements.md`** — the sprint plan and Definition of Done.
  The **Sprint 3** section ("Variables, Scope and REPL") is the scope contract: what
  ships in this sprint and what does not. §3.1.1 covers the day-one LSP properties the
  type checker populates for every new identifier node. §3.3 is the OpCode enum. §3.5
  routes tests to the right project. The Sprint 3 acceptance block is the bar — and
  note that it does **not** include the calculator smoke test (that is Sprint 4's
  gate; see §3).
- **`docs/design/grob-language-fundamentals.md`** — the live spec for the type rules.
  **§24** (`const` and `readonly` semantics) is the spec for Increment C, including the
  compile-time-constant allowlist and the deep-immutability rules. **§21** (Optional
  Chaining and Nil Propagation) governs `?.` short-circuit. The scope-chain rules, the
  `:=`/`=`/compound/`++` assignment rules and the string-interpolation `${...}` rule
  (including nullable interpolation) all live here.
- **`docs/design/grob-type-registry.md`** — the built-in type registry; `GrobType`,
  the nil/nullable representation, the compiler-internal `Error` type and the
  display/`toString` rule for interpolation slots.
- **`docs/design/grob-vm-architecture.md`** — the VM, the value stack, `GrobValue`,
  the globals table, the locals/slots model and the jump-offset mechanics.
- **`docs/design/grob-error-codes.md`** — the error registry. Sprint 3 raises E0101,
  E0102, E0104, E0201, E0202, E0203, E0204, E1001, E5201 and registers **E0205** and
  **E0206** this sprint. The numbering scheme is ADR-0014; stability is ADR-0017.
- **`docs/design/grob-solution-architecture.md`** — the project graph and dependency
  rules. The Compiler/Vm non-reference is the load-bearing one. Note: the REPL lives
  in `Grob.Cli` (`ReplCommand`), **not** in a separate project.
- **`docs/design/grob-benchmarking-strategy.md`** — the canonical baseline-production
  path (the `benchmark.yml` workflow, D-309) and what the Sprint 3 benchmark addition
  is (the first VM-execution category and its baseline), versus what is still deferred.

Published wiki versions of some of this material also exist under `docs/wiki/`. The
wiki is a reference rendering; **`docs/design/` is the working corpus and the
decisions log is the authority.** If they disagree, prefer the design docs and flag
the drift.

If the repository tree differs from the above — different directory, different
filenames, files missing — **stop and report the actual layout** rather than guessing.
A missing spec file is itself a finding.

---

## 1. What Sprint 3 delivers, in one paragraph

Sprint 2 delivered the back end for an expression-only surface (`print(2 + 3 * 4)` →
`14`). Sprint 3 makes the language declare, scope, reassign and run script files,
across six increments: **A** — mutable declarations and assignment (`:=`, `=` walking
the scope chain), the globals table (`DefineGlobal`/`GetGlobal`/`SetGlobal`), locals on
stack slots (`GetLocal`/`SetLocal`), block scoping with `PopN`, type annotations,
`++`/`--` (`IncrementInt`/`DecrementInt`) and compound assignment; **B** — `grob run
<file>`, a thin CLI wrapper over the pipeline; **C** — `const` (compile-time constant,
inlined, no runtime slot) and `readonly` (runtime-once, deep-immutable), sharing one
AST node, with E0205/E0206 newly registered; **D** — the nullable runtime: `T?` rules,
`??` (`NilCoalesce`), `?.` optional-chaining short-circuit, `IsNil` and the reusable
forward-jump backpatching helper; **E** — string-interpolation compilation
(`BuildString`) with the nullable-interpolation compile error; **F** — the REPL
(`grob repl`), the end-to-end `grob run hello.grob` close-gate and the first
VM-execution benchmark baseline. The acceptance bar is the Sprint 3 acceptance block in
the requirements doc: variables declared / reassigned / used; `const`/`readonly`
semantics enforced; nil safety working; string interpolation working; the REPL working;
`grob run hello.grob` executing a script file. Control flow, functions and user types
are Sprint 4 and later. Do not test for them.

---

## 2. The single most important instruction: report, do not patch

This codebase reflects deliberate design decisions backed by the decisions log. Many
things that look like bugs or smells are settled. **Your default action is to file a
finding, not to change code.** Before filing anything as a defect, search
`grob-decisions-log.md` for a relevant `D-###` entry.

Two buckets, and every finding goes in exactly one:

- **CORRECTNESS** — the code does not do what a cited spec section says it must.
  A `const` that emits a `DefineGlobal` and a runtime slot instead of inlining, a
  `readonly` array that accepts `.append`, a `PopN` that discards the wrong count, an
  `a ?? b` that skips evaluating `b`, a `?.` chain that dereferences past a nil
  receiver, a `ResolvedType` or `Declaration` that is null/unset after type-check, a
  `Grob.Cli` that swallows a runtime error and exits `0`, an `"E0205"` literal at a
  call site instead of a catalog descriptor. You may propose a patch, clearly labelled
  as a proposal.
- **SEMANTIC / DESIGN** — anything where you are questioning _what the language does_
  rather than _whether the code matches spec_. **Report only. Never patch. One line
  each.** Assume it is deliberate and that a decision-log entry exists. If you can cite
  the `D-###` that settles it, note it and move on; that is not a finding at all.

If you are unsure which bucket a finding belongs in, it goes in SEMANTIC / DESIGN.

---

## 3. Scope — what is in Sprint 3 and what is not

The authoritative scope is the Sprint 3 section of `grob-v1-requirements.md`. Read it.
The summary below orients you; the document governs.

### In scope (test these)

- **Declarations and assignment (A).** `:=` declaration and inference;
  re-`:=` of a name in the same scope is a compile error; `=` resolving by walking the
  scope chain inner-to-outer; assignment to an undeclared name is a compile error; the
  globals table and `DefineGlobal`/`GetGlobal`/`SetGlobal`; locals on stack slots and
  `GetLocal`/`SetLocal`; block scoping and `PopN`; type annotations
  (`name: Type := value`) and annotation-mismatch errors; `++`/`--` (`int` only) and
  compound assignment (`+=` etc.) desugaring.
- **`grob run` (B).** Reads and runs a `.grob` file; results to stdout, diagnostics to
  stderr; compile errors abort before execution; runtime errors stop on first; exit
  codes; clean handling of a missing/unreadable file.
- **`const` and `readonly` (C).** The shared `SingleAssignmentDeclaration` node and its
  `Kind` discriminator; the `const` compile-time-constant evaluator and its allowlist
  (E0205 for a non-constant RHS); `const` inlining with **no runtime slot**; the
  cross-reference rules (E0206 for `const` referencing a `readonly`); `readonly`
  runtime-once with deep immutability (E0202 reassignment, E0204 mutation); `param`
  implicitly `readonly` (E0203); the E0205/E0206 registry and `ErrorCatalog` additions.
- **Nullable runtime (D).** `T?` type rules (E0101 nil deref without `?.`/`??`, E0104
  nullable where non-nullable required); `??` via the eager `NilCoalesce` opcode; `?.`
  short-circuit via `IsNil` plus backpatched forward jumps; the reusable
  `emitJump`/`patchJump` helper; runtime nil dereference (E5201).
- **String interpolation (E).** Compilation of the parsed interpolation parts to
  ordered fragment pushes plus `BuildString N`; the nullable-interpolation compile
  error (E0102); the display/`toString` conversion at non-string slots.
- **REPL and close (F).** `grob repl` — the `G>` prompt, auto-printed expression
  results, multi-line input, history, persistent session scope, report-and-continue on
  error; the `grob run hello.grob` close-gate; the VM-execution benchmark category and
  its committed baseline.
- **The §3.1.1 invariant, extended.** Every identifier node introduced across A–E
  carries a non-null `ResolvedType` and a non-null `Declaration` after type-check (the
  `UnresolvedDecl.Instance` sentinel at error paths, D-311).
- **The full Sprint 1 and Sprint 2 surface, lightly** — Sprint 3 must not have
  regressed it. `print(2 + 3 * 4)` → `14` still holds; the §29.6 worked example still
  produces its full diagnostic count.

### Explicitly OUT of scope (do not test, do not flag as missing)

- **Control flow.** `if`/`else`/`while`/`for`/`select`/switch expressions, the general
  use of `Jump`/`JumpIfFalse`/`JumpIfTrue`/`Loop` for statements — **Sprint 4.** Note
  the subtlety: Increment D _does_ use `IsNil` + the jump opcodes and the backpatching
  helper to compile `?.` short-circuit. That partial jump usage is correct and in
  scope. The absence of `if`/`while`/`for` _statements_ is not a finding.
- **Flow-sensitive narrowing.** `if (x != nil) { … x … }` narrowing `x` from `T?` to
  `T` is **Sprint 5.** Consequently, in Sprint 3 the _only_ way to resolve a nullable
  for a non-nullable context (including a nullable interpolation slot) is
  `?? <fallback>`. A finding "narrowing does not resolve E0102" is noise — narrowing
  does not exist yet by design.
- **Functions, lambdas, call frames, closures, upvalues.** Sprint 5.
- **User types, structs, anonymous structs, the `type` keyword.** Sprint 6. The
  `readonly` deep-immutability rule _names_ struct-field and array-index mutation as
  things to reject when the target is `readonly`; rejecting those forms is in scope,
  but implementing struct or collection _semantics_ is not. Do not flag "structs are
  not implemented".
- **A separate REPL project.** The REPL lives in `Grob.Cli` (`ReplCommand`) by design
  (see the architecture doc). A finding "the REPL should be its own `Grob.Repl`
  project" misreads the architecture.
- **The calculator smoke test.** It exercises `while` loops and functions and is
  **Sprint 4's** acceptance gate, not Sprint 3's. Sprint 3 closes on
  `grob run hello.grob` over a declaration / scope / `const`-`readonly` / nil-safety /
  interpolation script. A finding "the calculator does not run" is noise. Discard it.
- **Standard library modules** beyond the named constant whitelist the `const`
  allowlist references; **plugins, the LSP, the formatter, `grob check`/`fmt`/
  `install`**.
- **The full benchmark suite / stability test.** Sprint 3 adds _one_ new category
  (VM-execution) and its baseline, via the workflow. A finding "the regression gate is
  not calibrated" or "there is no plugin benchmark" is out of scope. Confirm against
  `grob-benchmarking-strategy.md`.
- **Per-error-code long-form documentation** (`docs/errors/Exxxx.md`). Scheduled before
  v1 release. Absence is not a Sprint 3 finding.

A finding of the form "there is no `if` statement" or "functions are not implemented"
is noise. Discard it.

---

## 4. The Sprint 3 invariants — highest-value targets

These are the non-deferrable Sprint 3 acceptance criteria and the structural pieces
later sprints build on. Confirm their exact wording in the requirements doc and the
relevant spec section before asserting. Prefer a written assertion or a test over an
eyeball read for every one.

### 4.1 The §3.1.1 invariant holds across every new identifier node

Sprint 2 made `ResolvedType` and `Declaration` non-null after type-check for the
expression surface. Sprint 3 introduces declaration, assignment, `const`/`readonly`,
nullable and interpolation identifier nodes — the invariant must extend to all of them.
Walk a parsed-and-type-checked AST for a representative source file that uses every
Sprint 3 form and assert **every identifier node carries a non-null `ResolvedType` and
a non-null `Declaration`**. Confirm what "non-null" means: per D-311 the declaration
side uses an `UnresolvedDecl.Instance` singleton sentinel at error paths rather than
literal `null`, so the assertion is "every identifier resolves to _something_, and that
something is the real declaration where resolution succeeded, or the sentinel where it
did not — never an unset default". Assert the sentinel by reference (`Assert.Same`),
not by value. A genuinely null/unset `Declaration` or `ResolvedType` is a CORRECTNESS
finding.

### 4.2 The scope chain and `PopN` are exactly right

The stack discipline is the foundation Sprint 4/5 build on; an off-by-one corrupts
every later frame. Verify empirically:

- `=` resolves inner-to-outer: a reassignment inside a block targets the
  nearest-enclosing binding of that name, and a shadow in the block does not leak out.
- Re-`:=` of a name already declared in the current scope is a compile error; `=` to a
  name never declared is a compile error.
- A `{ }` block's locals are not visible after the block, and the compiler emits a
  single `PopN` discarding **exactly** the block's locals — not one too many (which
  pops a live value) or one too few (which leaks a slot). Compile a fixture with nested
  blocks, disassemble and count.

### 4.3 `const` is inlined with no runtime slot; the allowlist is enforced

This is the load-bearing distinction from `readonly`. Verify empirically:

- Compile a `const` and a reference to it; disassemble. There must be **no**
  `DefineGlobal`/`DefineLocal` for the `const`, and the reference must be a `Constant`
  pool load. A `const` that occupies a runtime slot is a CORRECTNESS finding.
- The compile-time-constant evaluator folds the allowlist forms (literals, arithmetic /
  comparison / logical on constants, unary, string concat, `const` references, the
  named stdlib constant whitelist) and rejects everything else with **E0205** — confirm
  the allowlist in §24, then throw adversarial RHS at it: a function call (including a
  stdlib function like `math.min(1, 2)`), a struct/array/map literal, an interpolated
  string, a `??`/`?.`/lambda/ternary, an `env.*`/`date.*` call. Each must be E0205.
- A `const` referencing a `readonly` identifier is **E0206** specifically (not E0205).
  A `readonly` referencing a `const` is valid.

### 4.4 `readonly` deep immutability is enforced

Confirm the rules in §24 / D-291, then verify empirically with adversarial inputs:

- Reassigning a `readonly` is E0202; reassigning a `param` is E0203.
- Deep mutation is E0204: for a `readonly` array, `.append(x)` and `arr[i] = v`; for a
  `readonly` map, `map[k] = v`; for a `readonly` struct, `point.field = v`; and `++` /
  `--` / `+=` on a `readonly int`. A `readonly` that admits any of these is a
  CORRECTNESS finding.
- A `readonly` compiles to the **same** `DefineGlobal`/`DefineLocal` path as a mutable
  binding (it occupies a normal slot/global); only the compile-time checks differ. A
  `readonly` that compiles to a special opcode is a finding against the spec.

### 4.5 `??` evaluation order matches the spec exactly

The `NilCoalesce` opcode is defined as eager — "pop two, push right if left is nil" —
and `a ?? b` therefore evaluates **both** operands; `b` is not skipped when `a` is
non-nil. **First confirm the current spec/decision state** (the operator table in the
fundamentals, and search the decisions log for any entry governing `??` evaluation
order — there may now be one). Then verify the **code matches that state**:

- Compile `a ?? b`; disassemble. Per the eager spec it must be
  `compile(a); compile(b); NilCoalesce`, with **no** jump for `??`. A jump-based
  short-circuit lowering of `??` contradicts the eager opcode and is a CORRECTNESS
  finding against the spec-as-written.
- If you believe `??` _should_ short-circuit its right operand (the C# / JS / Swift
  behaviour), that is a SEMANTIC / DESIGN observation — one line — not a CORRECTNESS
  finding, unless the decisions log now says short-circuit and the code does eager (or
  vice versa), in which case the **code-vs-log mismatch** is the CORRECTNESS finding.

### 4.6 `?.` short-circuits the whole chain, with correct backpatched offsets

§21 requires `a?.b?.c` to yield `nil` the instant any receiver is nil, attempting no
further access. There is no `?.` opcode — it is `IsNil` plus forward jumps. Verify
empirically:

- A `?.` chain whose early receiver is nil yields `nil` and does **not** execute the
  later steps (use a fixture where a later step would have an observable effect or would
  error if reached). A chain that dereferences past a nil receiver is a CORRECTNESS
  finding.
- The backpatched jump targets are correct. Compile `a?.b?.c`, disassemble and confirm
  each forward jump lands on the intended instruction — an off-by-one in `patchJump`
  sends control to the wrong opcode and is a CORRECTNESS finding that will not always
  show up as a wrong _value_.

### 4.7 The reusable jump primitive and the slot model are shaped for Sprint 4/5 reuse

Two structural pieces in this sprint are load-bearing for the next:

- The **`emitJump`/`patchJump` backpatching helper** (Increment D). Sprint 4's
  `if`/`while`/`&&`/`||` reuse it. Confirm it is a reusable compiler primitive, not a
  one-off inlined into the `?.` path. A correct-but-unreusable implementation is a
  SEMANTIC observation worth raising (it forces Sprint 4 to reinvent it).
- The **local-slot allocation and block-scope tracking** (Increment A). Sprint 5's call
  frames and upvalues build on it. This is harder to assert mechanically; read it cold
  against the clox model and the §-scope rules and note any shape that will not extend.

### 4.8 `ErrorCatalog` parity holds and no code numbers were invented (D-308)

Every Sprint 3 diagnostic raises through a central `ErrorCatalog` descriptor; the
`"Exxxx"` string for any code appears exactly once in the solution (its descriptor).
Verify:

- `"E0205"` and `"E0206"` each appear exactly once and are registered in
  `grob-error-codes.md` at the numbers the registry assigns (next-free in the
  `const`/`readonly` block), with the §24 wording. A duplicated literal, an unregistered
  code, or a code raised by a string literal at a call site is a CORRECTNESS finding.
- No invented codes — every code a Sprint 3 diagnostic raises exists in the registry.
  Grep for `"E0` / `"E1` / `"E5` literals outside the catalog; any hit is a finding.
- Confirm whether re-`:=`-in-scope, assign-to-undeclared and `float++` reuse existing
  codes (E1001 etc.) or were assigned new ones; either is fine, but an _invented_ or
  _unregistered_ number is a finding.

### 4.9 The close-gate and the negative paths

- `grob run hello.grob` through the real CLI entry point produces the expected stdout
  for the Sprint 3-surface script (declarations, scope, `const`/`readonly`, `??`/`?.`,
  interpolation). A wrong answer is the highest-priority CORRECTNESS finding in the
  sprint. Confirm `hello.grob` stays within the Sprint 3 surface — if it uses `while`
  or a function call, that is itself a finding (the gate is testing Sprint 4 features
  early).
- The inverse, for each new diagnostic class: a non-constant `const` RHS (E0205), a
  `readonly` mutation (E0204), a nullable interpolation (E0102) each produce a
  compile-time diagnostic with a line number and **do not execute**. A diagnostic that
  escapes to runtime, or a script that runs despite a compile error, is a CORRECTNESS
  finding.

### 4.10 The VM-execution benchmark baseline came from the workflow (D-309)

Sprint 3 adds the first VM-execution benchmark category. Confirm the committed baseline
JSON for that category was produced via the `benchmark.yml` GitHub Actions workflow on
the canonical runner — not by a local `dotnet run -c Release`. The commit should record
the run ID and runner. A locally produced committed baseline, or a baseline on a
different runner type from the existing compile-time baseline, is a CORRECTNESS finding
(it voids cross-run comparison). The category building and running is in scope; the
regression-gate calibration is not.

---

## 5. The two sprint-specific corrections to verify, not re-litigate

- **`??` is eager, per the spec as written.** §4.5 is the test. Your job is to confirm
  the code implements what the spec/log currently says, and to flag a code-vs-spec
  mismatch — not to argue the language should short-circuit. If you think it should,
  that is one SEMANTIC line.
- **E0205 and E0206 were assigned this sprint.** They are new registry entries
  (next-free in the `const`/`readonly` block, ADR-0014 numbering, ADR-0017 stability).
  Verify they exist, are referenced via catalog descriptors and carry the §24 wording.
  A finding "these codes did not exist before" is expected — they are new; the question
  is whether they are now correctly registered and used.

A finding of the form "this `:=` could be a collection expression" or "this method could
be a primary constructor" is a stylistic-modernisation suggestion and is out of scope
per §7. The build is the detector for real C# 14 / .NET 10 defects (D-310): build clean
in Debug and Release and read the warning list; anything that only a real defect would
surface is in scope, stylistic modernisation is not.

---

## 6. The bisection tools still apply

The disassembler (Sprint 2 A) is the localisation tool for this sprint's bytecode
work. Where an end-to-end script gives a wrong answer, compile the fixture, disassemble
the `Chunk` and read it: the `const` inlining (no slot), the slot indices and `PopN`
counts, the `??` eager sequence, the `?.` backpatched offsets and the `BuildString`
fragment counts are all visible there. Prefer a fixture-and-disassemble approach over
inspection for every bytecode-shape claim in §4 — the compiler is where the bugs live
(per the SharpBASIC retrospective the corpus references), and the disassembler exists
precisely to make compiler-side faults unambiguous.

Feed the disassembler the new opcode operands too (a `GetLocal`/`SetLocal` slot index,
a `PopN` count, a `BuildString` count, a backpatched jump offset) and confirm the
listing resolves them readably; a disassembler that prints the new operand-bearing
opcodes incorrectly is a CORRECTNESS finding, because it defeats the bisection it
exists for.

---

## 7. Things that may look wrong but are likely deliberate

Before filing any of the following as CORRECTNESS, find the governing `D-###` in
`grob-decisions-log.md`. If the log confirms the behaviour, it is not a finding; if the
code contradicts the log, _that_ is the finding. Non-exhaustive list of Sprint 3 areas
where a cold reader's instinct misfires:

- **`const` has no runtime slot — it is inlined at every reference.** D-288/D-289
  settle this; it is the deliberate difference from `readonly`. A finding "`const`
  should define a global like `readonly`" misreads the design.
- **`readonly` uses the same `DefineGlobal`/`DefineLocal` opcodes as a mutable
  binding.** Only the compile-time immutability checks differ; there is no special
  `readonly` opcode. Not a finding.
- **`??` is eager (no short-circuit).** Per the opcode definition and the operator
  table. See §4.5 — verify the code matches; do not redesign.
- **`?.` is the _only_ short-circuiting construct in Sprint 3.** `&&`/`||`
  short-circuit is Sprint 4. The jump opcodes and the backpatching helper exist and are
  used by `?.`; their absence from `if`/`while` is correct (those statements are
  Sprint 4).
- **`++`/`--` are `int` only.** `float++`/`float--` are compile errors. The `OpCode`
  enum carries `IncrementFloat`/`DecrementFloat` for completeness, but Sprint 3 does
  not wire them. An unwired `IncrementFloat` is not a finding; a wired one that makes
  `float++` compile _is_ (against the fundamentals).
- **The `Declaration` invariant uses the `UnresolvedDecl.Instance` singleton sentinel,
  not literal `null` (D-311).** Symmetric with `GrobType.Error` on the type side.
  Assert by reference. A finding "this should be `null`" misreads the invariant.
- **Nullable interpolation (E0102) is resolvable only by `??` in Sprint 3.** Narrowing
  is Sprint 5. Not a finding.
- **The REPL lives in `Grob.Cli`, not a separate project.** Architecture doc. Not a
  finding.
- **The calculator smoke test is Sprint 4's gate.** Sprint 3 closes on `hello.grob`.
  Absence of a calculator end-to-end is not a finding.
- **The compiler is partial classes by concern.** Declaration / assignment / nullable /
  interpolation emission added across files; the shape is laid down progressively.

The rule is the same throughout: the decisions log is the authority. If your instinct
says "this language should do X differently", that is a SEMANTIC / DESIGN observation at
most — one line, no patch — and if the log already settles it, it is nothing.

---

## 8. How to run the work

1. Build the solution in both Debug and Release (`dotnet build`,
   `dotnet build -c Release`). Clean build is the baseline; note warnings, prioritise
   only those indicating a real defect (the C# 14 / .NET 10 check, §5, lives here).
2. Run the existing test suite (`dotnet test`). Report any failure verbatim — a failing
   test in delivered code is a high-priority CORRECTNESS finding.
3. Run `grob run` against fixtures: the close-gate `hello.grob` (expect the Sprint 3
   output), a compile-error fixture (expect a diagnostic on stderr, non-zero exit, no
   execution), a runtime-error fixture (expect the error on stderr, non-zero exit), a
   missing-file path (expect a clean message, not a stack trace) and an `exit(2)`
   script (expect exit code 2).
4. Drive the REPL through `ReplCommand` with scripted input and captured writers (not a
   live terminal): a bare expression prints its value; a declaration does not; a later
   entry sees an earlier entry's declaration (persistent scope); a multi-line `{ }`
   block is read to completion before compiling; a compile error reports and the REPL
   continues.
5. Compile-and-disassemble fixtures per §6 for every bytecode-shape claim: `const`
   no-slot, slot indices and `PopN` counts, `??` eager sequence, `?.` backpatched
   offsets, `BuildString` counts.
6. Run the VM-execution benchmark category and confirm its baseline-production path is
   the workflow, not a local invocation (§4.10).
7. Read the type checker and compiler cold against §24, §21 and the scope/assignment
   rules. Write your own adversarial inputs per §4 — prefer adding throwaway xUnit tests
   in the existing test projects (routed per §3.5) over scratch harnesses, so assertions
   are reproducible and stick to the project the bug lives in.
8. Verify the invariants in §4 with assertions, not inspection, wherever practical. The
   `const` no-slot (§4.3), the `PopN` counts (§4.2), the `??` eager bytecode (§4.5) and
   the `?.` backpatch offsets (§4.6) only submit to empirical verification — eyeballing
   the source is not enough.

---

## 9. Output format

Produce a single findings document. No prose preamble and no summary of how Grob works
— that is all in the repo. For each finding:

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
SEMANTIC as a flat list. If a section of this brief produced no findings, say so in one
line — "§4.6 `?.` backpatch offsets: verified correct, no findings" is useful signal.
Where you relied on a spec file that was missing or differently located than §0
expected, say so explicitly at the top of the document.

Do not fix anything in the SEMANTIC bucket. Do not redesign. Find where the code lies
to the spec — as the spec actually reads in the repo, not as this brief paraphrases it
— and tell me precisely where.
