# Grob — Sprint 2 External QA Brief

> **For:** GPT-5.3-Codex (or equivalent agentic model with terminal access)
> **Role:** Independent cold-reader and adversarial tester of the Grob Sprint 2 implementation
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
  other document and outranks this brief.
- **`docs/design/grob-v1-requirements.md`** — the sprint plan and Definition of Done.
  The Sprint 2 section is the scope contract: what ships in this sprint and what does
  not. §3.1.1 covers the day-one LSP properties the type checker now populates. §3.3
  is the OpCode enum and the typed-opcode rationale. §3.5 routes tests to the right
  project. §4 is the Sprint 2 acceptance bar.
- **`docs/design/grob-language-fundamentals.md`** — the live spec for the front end
  and the type rules. §29 (parser error recovery) is now relevant on _both_ halves —
  the parser produces error nodes, and the type checker resolves them via the
  compiler-internal `Error` type. Read both halves.
- **`docs/design/grob-vm-architecture.md`** — the VM, the value stack, `GrobValue`,
  the `Chunk`, the constant pool, and the **"Developer Diagnostics"** section that
  specifies the disassembler output and the `#if DEBUG` trace hook.
- **`docs/design/grob-grobc-format.md`** — the `.grobc` binary format skeleton (D-298).
- **`docs/design/grob-solution-architecture.md`** — the project graph and dependency
  rules. The Compiler/Vm non-reference is the load-bearing one.
- **`docs/design/grob-type-registry.md`** — the built-in type registry. Contains the
  entry for the compiler-internal `Error` type used for cascade suppression, and the
  built-in scalar type names (lowercase per a `D-###` you should look up rather than
  taking from this brief).
- **`docs/design/grob-benchmarking-strategy.md`** — what the Sprint 2 benchmark
  skeleton must contain at sprint close, and what is explicitly deferred to later
  sprints (the stability test, the full suite, the regression gate calibration).

Published wiki versions of some of this material also exist under `docs/wiki/`
(`Language-Specification/`, `Type-Registry/`, `ADR/`, etc.). The wiki is a reference
rendering; **`docs/design/` is the working corpus and the decisions log is the
authority.** If the wiki and the design docs ever disagree, prefer the design docs and
flag the drift.

If the repository tree differs from the above — different directory, different
filenames, files missing — **stop and report the actual layout** rather than guessing
or testing against assumptions. A missing spec file is itself a finding.

Once you have read those, the rest of this brief tells you how to attack the code.

---

## 1. What Sprint 2 delivers, in one paragraph

Sprint 1 delivered the front end (lexer, error-recovering parser, AST, diagnostics).
Sprint 2 delivers the back end: the **bytecode primitives** (`OpCode`, `GrobValue`,
`Chunk`) and the **always-compiled disassembler** (Increment A); the **stack-based VM
dispatch loop** with checked arithmetic and the `#if DEBUG` trace hook (Increment B);
the **two-pass type checker** that annotates the AST with resolved types and
declaration back-references (Increment C); and the **compiler core** that emits typed
opcodes from those annotations, plus the **end-to-end integration test** and the
**BenchmarkDotNet skeleton** (Increment D). The acceptance bar is
`print(2 + 3 * 4)` → `14`, end to end, with type errors caught at compile time.
Declarations, control flow, functions and user types are Sprint 3 and later. Do not
test for them.

---

## 2. The single most important instruction: report, do not patch

This codebase reflects deliberate design decisions backed by the decisions log. Many
things that look like bugs or smells are settled. **Your default action is to file a
finding, not to change code.** Before filing anything as a defect, search
`grob-decisions-log.md` for a relevant `D-###` entry.

Two buckets, and every finding goes in exactly one:

- **CORRECTNESS** — the code does not do what a cited spec section says it must.
  A `ResolvedType` that is null after type-check on an identifier node, a typed-opcode
  selection that picks `AddInt` for `float + float`, a VM that silently wraps on `int`
  overflow instead of throwing, a `Grob.Vm` `ProjectReference` to `Grob.Compiler`, an
  emitted bytecode whose line array disagrees with the source. You may propose a
  patch, clearly labelled as a proposal.
- **SEMANTIC / DESIGN** — anything where you are questioning _what the language does_
  rather than _whether the code matches spec_. **Report only. Never patch. One line
  each.** Assume it is deliberate and that a decision-log entry exists. If you can
  cite the `D-###` that settles it, even better — note it and move on; that is not a
  finding at all.

If you are unsure which bucket a finding belongs in, it goes in SEMANTIC / DESIGN.

---

## 3. Scope — what is in Sprint 2 and what is not

The authoritative scope is the Sprint 2 section of `grob-v1-requirements.md`. Read it.
The summary below orients you; the document governs. Note that §4 in the requirements
doc still describes Sprint 2 as a single monolithic increment with one acceptance
block; the implementation slices it into four increments (A primitives + disassembler,
B dispatch loop, C type checker, D compiler + end-to-end + benchmark skeleton). Both
views are accurate — the §4 acceptance is met when all four increments have landed.

### In scope (test these)

- `OpCode`, `GrobValue`, `Chunk` in `Grob.Core` — shapes, write surfaces, the
  encapsulated factory/accessor surface of `GrobValue`.
- The `Disassembler` in `Grob.Vm` — output format, byte offsets, opcode names,
  constant resolution, the shared-line marker convention, behaviour on malformed
  bytecode.
- The VM dispatch loop in `Grob.Vm` — `ValueStack`, fetch-decode-execute, the
  arithmetic opcode family, `Constant` / `Print` / `Return`, runtime error surface and
  source-line plumbing, the `#if DEBUG` trace hook.
- The type checker in `Grob.Compiler` — two-pass structure, type inference on `:=`,
  arithmetic and comparison type rules, the §3.1.1 invariants (`ResolvedType` and
  `Declaration` non-null on every identifier node after type-check), collect-all-errors
  semantics, cascade suppression via the compiler-internal `Error` type.
- The compiler in `Grob.Compiler` — typed-opcode selection from `ResolvedType`,
  constant-pool management, per-instruction line tracking, `print()` and `exit()` as
  resolved-at-type-check built-ins.
- The end-to-end pipeline — source → tokens → AST → typed AST → `Chunk` → executed →
  stdout, for the acceptance input `print(2 + 3 * 4)`.
- The benchmark skeleton — `bench/Grob.Benchmarks/`, the seed compile-time benchmark,
  the first committed baseline JSON, the documented `dotnet run -c Release` invocation.
- Test routing per §3.5 of the requirements doc — VM tests in `Grob.Vm.Tests`,
  compiler/type-checker tests in `Grob.Compiler.Tests`, the end-to-end test in
  `Grob.Integration.Tests`.
- The full Sprint 1 surface, lightly — Sprint 2 must not have regressed it.

### Explicitly OUT of scope (do not test, do not flag as missing)

- **Declarations.** `:=`, `=`, `const`, `readonly`, the globals table, `DefineGlobal`,
  `GetGlobal` — Sprint 3. The Sprint 2 type checker handles `:=` _inference_ as part
  of expression typing, but full declaration semantics with reassignment / scope /
  shadowing rules land later. Confirm the boundary by reading the Sprint 3 section of
  the requirements doc; do not assume.
- **Control flow.** `if`/`else`/`while`/`for`/`select`/switch expressions, the
  `Jump`/`JumpIfFalse`/`Loop` opcodes — Sprint 4. The opcodes exist in the `OpCode`
  enum from Increment A (the full enum is written out, not grown), but they have no
  dispatch arm and no emission path yet. That is correct; absent dispatch for a
  Sprint-4 opcode is not a finding.
- **Functions, lambdas, call frames, closures.** Sprint 5. The Sprint 2 `Return`
  opcode ends the top-level chunk only; there is no `Call` and no frame stack yet.
- **User types, structs, anonymous structs, the `type` keyword.** Sprint 6.
- **Standard library modules, plugins, the LSP, the formatter, the REPL beyond what
  Sprint 2 needs.**
- **The full benchmark suite.** Sprint 2 ships only the _skeleton_ plus the first
  compile-time baseline. The stability test and its calibration are Sprint 8 per the
  benchmarking strategy. A finding "there is no VM-execution benchmark" is noise — the
  scope is the skeleton, not the suite. Confirm against `grob-benchmarking-strategy.md`.
- **Per-error-code long-form documentation** (`docs/errors/Exxxx.md`). Scheduled for a
  dedicated session before v1 release. Absence is not a Sprint 2 finding.

A finding of the form "there is no globals table" or "there is no `if` statement" is
noise. Discard it.

---

## 4. The day-one invariants — highest-value targets

These are the non-deferrable Sprint 2 acceptance criteria. Confirm their exact wording
in the requirements doc and the relevant spec section before asserting. They are the
most expensive things to retrofit and therefore the most worth verifying
_empirically_. Prefer a written assertion or a test over an eyeball read for every one.

### 4.1 The §3.1.1 invariants are now _populated_, not just present

Sprint 1 left `IdentifierExpr.ResolvedType` and `IdentifierExpr.Declaration` as
shapes-without-values — the QA-fix made them exist and be settable, with the
populated-after-type-check assertion deferred to Sprint 2 as a pending test. Sprint 2
must now make that test _pass_. Write a visitor that walks a parsed-and-type-checked
AST for a representative source file and assert: **every identifier node carries a
non-null `ResolvedType` and a non-null `Declaration` after the type-check pass**. A
null on either is a CORRECTNESS finding. Confirm the exact invariant wording in
§3.1.1 before asserting — including what "non-null" means for `ResolvedType` if
`GrobType` is a value type (the resolution may be a dedicated `Unresolved` sentinel
rather than literal `null`; either way, the invariant is "every identifier resolves
to _something_ after type-check, and that something is not the default / unresolved
sentinel"). The Symbol table's `DeclaredAt` invariant likewise — every registered
symbol has a populated `DeclaredAt`.

### 4.2 Source location threads through every emitted instruction

Every instruction the compiler emits must record its source line in the chunk's
parallel line array. Build a small fixture, compile it, and verify line-array entries
match the source positions of the AST nodes that produced them. The disassembler reads
this array; the VM reads it for runtime-error attribution; an off-by-one here cascades
into wrong line numbers on every diagnostic the VM raises. Verify empirically.

### 4.3 The VM stops on the first runtime error; the type checker collects all

This is the two-mode error model. Verify both halves:

- Hand-construct (or compile) a chunk that would produce two runtime errors if it ran
  to completion, and assert the VM raises the _first_ and stops — does not collect the
  second.
- Write source that contains three independent type errors and assert the type
  checker reports all three from a single pass, not just the first.

A compiler/checker that stops on first is a CORRECTNESS finding. A VM that collects
runtime errors and continues is a CORRECTNESS finding. Cascade suppression in the type
checker (a derived diagnostic from an already-errored sub-expression _not_ being
emitted) is a separate concern — verify that independently per §4.5.

### 4.4 The §29.6 worked example now produces its _full_ diagnostic count

The Sprint 1 QA brief told you to verify only the parser's share of §29.6 because the
type checker did not yet exist. **In Sprint 2 the type checker exists**, so the §29.6
example should now produce the full diagnostic count §29.6 states — both the parser's
contribution and the type checker's undefined-identifier diagnostic. Reproduce the
§29.6 input, run it through the full pipeline (parse + type-check), and assert the
diagnostic count and shape exactly match §29.6's stated expected outcome. A pipeline
that produces fewer diagnostics than §29.6 specifies — or different ones — is a
CORRECTNESS finding.

If the parser-half of §29.6 has regressed from Sprint 1, that is its own finding.

### 4.5 Cascade suppression via the compiler-internal `Error` type

§29.3 specifies the contract: an expression built on an already-errored sub-expression
resolves to the compiler-internal `Error` type, which is universally assignable, so a
single mistake does not produce a storm of derived diagnostics. Write source that
errors once and then uses the errored binding in multiple downstream positions, and
assert there is _one_ type-error diagnostic, not several. Confirm the `Error`-type
contract in `grob-type-registry.md` before asserting — it is the existing mechanism;
verify the type checker reuses it rather than inventing a parallel suppression scheme.

### 4.6 `TraceInstruction` is absent from Release

D-306 specifies that the dispatch loop's `TraceInstruction` hook is `#if DEBUG` and
compiled out of Release entirely, so that the Release dispatch loop the benchmarks
measure has no trace branch on the hot path. Verify this _empirically_, not by reading
the `#if DEBUG`:

- Build the `Grob.Vm` project in Release configuration.
- Inspect the dispatch-loop IL (`ildasm`, `dotnet-ildasm`, or equivalent) and confirm
  there is no call to `TraceInstruction` and no branch testing a trace flag.
- A Release build that still contains the trace path is a CORRECTNESS finding — it
  silently invalidates the benchmark baseline D-302 commits.

A `#if DEBUG` written in source is necessary but not sufficient; a stray usage outside
the guard, or a non-trivial method call inside it whose call site survives, will
defeat the intent. The IL is the truth.

### 4.7 The `Grob.Vm` ↔ `Grob.Compiler` non-reference

The architecture doc names this as the load-bearing DAG invariant: the VM and the
compiler must never reference each other. Verify from the actual `.csproj` files (and
the solution file's project graph) that neither has a `ProjectReference` to the other,
in either direction. Anything in between — both referencing `Grob.Core`, both
referenced by `Grob.Cli` — is allowed and expected. A violation here is a CORRECTNESS
finding regardless of whether the code currently compiles.

### 4.8 No allocation on the `GrobValue` push path for primitives

D-303 and D-304 together pin the perf claim: pushing a primitive `GrobValue` onto the
value stack is a struct copy with no heap allocation. Verify empirically — a microtest
that pushes-and-pops integers in a tight loop and asserts the allocated-bytes counter
(`GC.GetAllocatedBytesForCurrentThread()` between samples, or a BenchmarkDotNet
`[MemoryDiagnoser]` run) reads zero. Boxing on the push path is a CORRECTNESS finding;
it makes the dispatch loop allocate per instruction, which voids the benchmark and
the whole point of the tagged-union struct.

### 4.9 The end-to-end acceptance

`print(2 + 3 * 4)` through the full pipeline — lex, parse, type-check, compile,
execute — produces stdout exactly `14`. Reproduce against the real binary or the
integration-test entry point. A wrong answer (`9`, `20`, anything other than `14`) is
the highest-priority CORRECTNESS finding in the entire sprint. Also verify the
inverse: `"hello" + 42` (or another deliberate type error) produces a compile-time
diagnostic with a line number and **does not execute**. A type error that escapes to
runtime is a CORRECTNESS finding.

---

## 5. The C# 14 / .NET 10 pinning correction

The project pinned C# 13 while building against .NET 10 (which ships C# 14) for part
of the sprint. The pinning has been corrected. **This is not an invitation to hunt for
"missed C# 14 opportunities"** — that is a stylistic-modernisation pass, and
stylistic findings are explicitly out of scope per §7.

The narrow check that _is_ in scope: build the solution with the corrected pinning
and confirm there are no warnings the C# 13 pinning had previously suppressed.
Anything that surfaces only under the corrected pinning is a CORRECTNESS finding;
anything that compiles clean under both is settled. The build is the detector here;
your only job is to verify it is green and to read the warning list (if any) for real
defects.

A finding of the form "this collection initialiser could be a collection expression"
or "this constructor could be a primary constructor" is noise. Discard it.

---

## 6. The disassembler-as-bisection-tool invariant

The disassembler exists, in part, because the implementation order (B before D)
relies on it for bisection. When the end-to-end test produces a wrong answer, the
disassembler localises the fault — VM-side or compiler-side — without ambiguity. Two
checks worth running:

- Compile a small fixture, disassemble the produced `Chunk`, and confirm the
  disassembled listing is _readable_ — byte offset, opcode name, constants resolved,
  source lines correct, shared-line marker working. A disassembler whose output is
  technically correct but unreadable defeats the invariant.
- Feed the disassembler a deliberately malformed byte sequence (an opcode byte outside
  the enum, a constant index out of range, a truncated operand). It must emit a clear
  "unknown opcode" / out-of-range line and continue, not throw. A debugging tool that
  crashes on the input you most want to debug is a CORRECTNESS finding.

---

## 7. Things that may look wrong but are likely deliberate

Before filing any of the following as CORRECTNESS, find the governing `D-###` in
`grob-decisions-log.md`. If the log confirms the behaviour, it is not a finding; if
the code contradicts the log, _that_ is the finding. This is a non-exhaustive list of
Sprint 2 areas where a cold reader's instinct misfires:

- **`GrobValue` is a tagged-union struct, not NaN boxing.** D-303 settles this; OQ-005
  recorded the deferred decision and its provisional resolution. The shape is locked.
  A SEMANTIC suggestion to switch representations is one line at most, and the log
  already addresses it.
- **The `OpCode` enum is fully written out from §3.3, not grown incrementally.** Same
  discipline as `TokenKind` in Sprint 1. Opcodes that exist but have no dispatch arm
  in Sprint 2 are deferred to later sprints — Sprint 3 (declarations) and Sprint 4
  (control flow) bring up their dispatch arms. Absence of a dispatch arm for a
  future-sprint opcode is not a finding.
- **Typed arithmetic opcodes (`AddInt` / `AddFloat` / `Concat`) rather than a single
  `Add` with a runtime type tag.** This is the whole point of the type-checker /
  compiler split: the checker proves the type, the compiler bakes the type into the
  opcode, the VM dispatches without runtime type checks. A SEMANTIC suggestion to
  collapse them is one line.
- **The disassembler is always-compiled; the trace hook is `#if DEBUG`-only.** These
  are different things with different lifetimes — the disassembler is a developer tool
  and a Sprint-12 CLI surface (`grob dump`); the trace hook is a benchmark-protecting
  inner-loop probe. Do not flag the asymmetry.
- **`int` overflow throws `ArithmeticError`; it does not wrap.** Checked arithmetic is
  the language semantics. The decisions log has the entry.
- **`int / int` truncates rather than producing `float`.** Settled. Do not flag.
- **The only implicit numeric conversion is `int → float`.** Everything else is
  explicit. Settled.
- **`print()` and `exit()` are not keywords.** They are built-ins resolved at
  type-check against the registered natives (D-270). The lexer treats them as
  identifiers and the type checker binds them. A finding "these should be `TokenKind`
  entries" misreads the design.
- **Built-in scalar type names are lowercase** (`int`, `float`, `string`, `bool`).
  Find the governing `D-###` and confirm.
- **The compiler is partial classes by concern.** Expression emission in one file,
  statement / control-flow / declaration emission (when added) in others. The shape
  is laid down in Sprint 2 and extended by every subsequent sprint.
- **The benchmark skeleton has one baseline and one seed benchmark.** Not a partial
  suite — the deliberate Sprint 2 deliverable per `grob-benchmarking-strategy.md`.

The rule is the same throughout: the decisions log is the authority. If your instinct
says "this language should do X differently", that is a SEMANTIC / DESIGN observation
at most — one line, no patch — and if the log already settles it, it is nothing.

---

## 8. How to run the work

1. Build the solution in both Debug and Release (`dotnet build`,
   `dotnet build -c Release`). Clean build is the baseline; note warnings, but
   prioritise only those indicating a real defect. The C# 14 / .NET 10 pinning check
   (§5) lives here.
2. Run the existing test suite (`dotnet test`). Report any failure verbatim — a
   failing test in delivered code is a high-priority CORRECTNESS finding.
3. Run the benchmark skeleton end-to-end via the documented invocation in
   `grob-benchmarking-strategy.md` (look for the `dotnet run -c Release --project
   bench/Grob.Benchmarks` line). Confirm it runs, produces output, and writes the
   first baseline JSON to the path the strategy doc names. A skeleton that does not
   run on a clean checkout is a CORRECTNESS finding.
4. Read the type checker and compiler cold against the spec sections in §0. Note
   divergence. The compiler is where the bugs live (per the SharpBASIC retrospective
   the design corpus references) — typed-opcode selection, constant-pool indices,
   line-array entries — so prefer a fixture-and-disassemble approach over inspection.
5. Write your own adversarial inputs per §4. Prefer adding throwaway xUnit tests in
   the existing test projects over scratch harnesses, so assertions are reproducible
   and the failures stick to the project the bug lives in (per §3.5 routing).
6. Verify the invariants in §4 with assertions, not inspection, wherever practical.
   §4.6 (Release IL) and §4.8 (zero allocation) are the two that _only_ submit to
   empirical verification — eyeballing `#if DEBUG` or `readonly struct` is not enough.

---

## 9. Output format

Produce a single findings document. No prose preamble and no summary of how Grob
works — that is all in the repo. For each finding:

```text
[CORRECTNESS | SEMANTIC] — one-line title
  Increment: A | B | C | D | cross-cutting
  File:      path:line
  Spec:      document and section, or "none / unsure"
  Decision:  D-### if you found a governing entry, or "none found"
  Observed:  what the code does
  Expected:  what the spec requires (CORRECTNESS only)
  Repro:     minimal input or test that demonstrates it (CORRECTNESS only)
  Patch:     proposed diff, labelled as a proposal (CORRECTNESS only, optional)
```

Order: all CORRECTNESS first, by severity (crash / wrong-output / minor), then all
SEMANTIC as a flat list. If a section of this brief produced no findings, say so in
one line — "§4.6 trace-hook absence from Release: verified clean, no findings" is
useful signal. Where you relied on a spec file that was missing or differently located
than §0 expected, say so explicitly at the top of the document.

Do not fix anything in the SEMANTIC bucket. Do not redesign. Find where the code lies
to the spec — as the spec actually reads in the repo, not as this brief paraphrases
it — and tell me precisely where.
