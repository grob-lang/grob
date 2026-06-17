# Grob — Sprint 4 External QA Brief

> **For:** GPT-5.3-Codex via Codex CLI (or an equivalent agentic model with
> terminal access to the repository)
> **Role:** Independent cold-reader and adversarial tester of the Grob Sprint 4
> (Control Flow) implementation
> **From:** Chris — Lead Developer, sole author of Grob
> **Purpose:** A second pair of eyes, from a different model family, that has
> never seen the design corpus. You are here to find divergence between what the
> code does and what the spec says it must do. You are _not_ here to redesign the
> language.

You are running in the repository as an agent with a terminal — check out the
merged Sprint 4 branch, read the tree, build, test, write your own adversarial
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
  every other document and outranks this brief. Sprint 4 leans on **D-277**
  (switch-expression pattern grammar), **D-301** (`select` non-exhaustive vs
  switch expression exhaustive), **D-303**, **D-308**, **D-309**, **D-310**,
  **D-313** and **D-314** (the Claude Code harness migration). Look them up.
- **`docs/design/grob-v1-requirements.md`** — the sprint plan and Definition of
  Done. **§5 (Sprint 4 — Control Flow)** is the scope contract: what ships and
  what does not. §3.1.1 is the day-one LSP invariant the type checker populates
  for every identifier node. §3.3 is the (closed) `OpCode` enum. §3.5 routes
  tests. The §5 acceptance block is the bar.
- **`docs/design/grob-language-fundamentals.md`** — the live spec for the
  constructs. **§1** (`if`/`else`), **§2** (`while`), **§3** (`select`/`case`,
  and "Why `select` is non-exhaustive"), **§3.1** (the switch **expression**),
  **§4** (`break`/`continue`), **§5** (`for...in`, including the numeric-range
  subsection), **§6** (Operators — Logical, for `&&`/`||`), **§7** (Operator
  Precedence — the ternary), **§12** (Expressions vs Statements).
- **`docs/design/grob-vm-architecture.md`** — the value stack, the forward `Jump`
  offset and the backward `Loop` offset mechanics, and how the dispatch loop
  advances over each.
- **`docs/design/grob-error-codes.md`** — the registry. Sprint 4 **registers**
  E2211/E2212 (`break`/`continue` outside a loop), E0501–E0504 (`for...in`
  iteration errors) and E0505 (non-exhaustive switch expression), and reuses the
  type-mismatch codes (E0001/E0002) for non-`bool` conditions and non-unifying
  arms. Numbering is ADR-0014; stability ADR-0017.
- **`docs/design/grob-type-registry.md`** — the `array`/`map<K, V>` surface the
  `for...in` lowering reads (length, `keys`, index), and arm-unification typing.
- **`docs/design/grob-benchmarking-strategy.md`** — §8/§9: the second
  VM-execution baseline production path (`benchmark.yml`, D-309) and the two-axis
  gate (`Grob.BenchCheck`, D-313).

Published wiki renderings may exist under `docs/wiki/`; the design docs are the
working corpus and the decisions log is the authority. If they disagree, prefer
the design docs and flag the drift.

---

## 1. What Sprint 4 delivers, in one paragraph

Sprint 3 made the language declare, scope, reassign and run scripts. Sprint 4
makes it branch and loop, across six increments over the type checker, the
compiler and the VM opcode arms — **no new `src/` project and no new opcodes**
(the control-flow opcodes were defined when the enum closed in Sprint 2 A, and
the `emitJump`/`patchJump` backpatching helper was built in Sprint 3 D and is
reused): **A** — `if`/`else if`/`else`, `&&`/`||` short-circuit and the ternary
`?:`, all forward-jump; **B** — `while` (the backward `Loop` jump) and
`break`/`continue` over a loop-context stack, with `break`/`continue`-outside-a-loop
at E2211/E2212; **C** — `for...in` in four forms (array, index `(i, item)`, map
`(k, v)`, numeric range with `step`/descending) lowered to `while`, with the
iteration errors E0501–E0504; **D** — the `select`/`case` **statement**:
first-match, no fall-through, `default`-optional and **non-exhaustive** (D-301),
an equality + `JumpIfFalse` ladder that pushes no loop context; **E** — the
switch **expression** `value switch { p => r, _ => d }`: value-producing,
**exhaustive** (type checker, E0505 for non-exhaustive), arms unifying to one
type; **F** — the loop-and-`select` `calculator.grob` close-gate (no user
functions), the §5 acceptance and the second VM-execution benchmark baseline. The
acceptance bar is the §5 block: all control-flow constructs work; array and map
`for...in` iterate; single-identifier map iteration is a clear compile error;
nested loops with `break`/`continue` behave; the switch expression is exhaustive;
`grob run calculator.grob` runs. Functions, closures, flow narrowing and user
types are Sprint 5 and later — do not test for them.

---

## 2. The single most important instruction: report, do not patch

This codebase reflects deliberate decisions backed by the decisions log. Many
things that look like bugs are settled. **Your default action is to file a
finding, not to change code.** Before filing anything as a defect, search
`grob-decisions-log.md` for a relevant `D-###`.

Two buckets, every finding in exactly one:

- **CORRECTNESS** — the code does not do what a cited spec section says it must.
  A `while` whose `Loop` offset is off by one, a `break` that escapes two loops
  instead of one, a `continue` in a lowered `for...in` that targets the condition
  instead of the increment step (so the counter never advances), a `select` that
  falls through to the next case, a switch expression that compiles non-exhaustively,
  a `&&` that evaluates its right operand when the left is `false`, a ternary that
  runs both arms, a `ResolvedType`/`Declaration` null after type-check, an
  `"E0501"` literal at a call site instead of a catalog descriptor. You may
  propose a patch, clearly labelled as a proposal.
- **SEMANTIC / DESIGN** — you are questioning _what the language does_ rather than
  _whether the code matches spec_. **Report only, never patch, one line each.**
  Assume it is deliberate and a decision-log entry exists. If you can cite the
  `D-###` that settles it, it is not a finding at all.

If unsure which bucket, it is SEMANTIC / DESIGN.

---

## 3. Scope — what is in Sprint 4 and what is not

The authoritative scope is §5 of `grob-v1-requirements.md`. Read it. The summary
below orients; the document governs.

### In scope (test these)

- **Conditionals (A).** `if`/`else if`/`else` with `bool` conditions; `&&`/`||`
  short-circuit (jump-based, no dedicated opcode); the ternary `?:` (an
  expression, arms unify); non-`bool` conditions/operands and non-unifying ternary
  arms are compile errors; every jump backpatched through the reused `patchJump`.
- **`while` + loop control (B).** `while` with a `bool` condition and the backward
  `Loop` jump; `break` (forward exit jump) and `continue` (to the loop-top for
  `while`); the loop-context stack; nested loops resolving control to the
  innermost; `break`/`continue` outside a loop are E2211/E2212.
- **`for...in` (C).** Array single and index forms; map `(k, v)` over the
  insertion-order keys; numeric range inclusive both bounds, `step`, descending;
  each lowered to `while`; `continue` targeting the increment step; the iterator
  variable immutable in the body; the iteration errors E0501 (non-iterable),
  E0502 (single-ident map → suggest `.keys`), E0503 (descending without negative
  `step`), E0504 (iterator reassignment).
- **`select`/`case` (D).** First-match, no fall-through, `default` optional,
  **non-exhaustive** (no match + no `default` is a no-op, not an error); the
  equality + `JumpIfFalse` ladder; multi-value `case Y, Z`; case-value type
  compatibility; **`select` pushes no loop context** so `break`/`continue` inside
  a case act on an enclosing loop.
- **Switch expression (E).** `value switch { p => r, _ => d }` — value-producing;
  **exhaustive** (E0505 for non-exhaustive); arms unify to one `ResolvedType`
  (the same unification as the ternary); the pattern grammar of D-277; every
  compiled path leaves exactly one value on the stack.
- **Calculator close (F).** `grob run calculator.grob` over the Sprint 1–4
  surface (declarations, scope, `const`/`readonly`, conditionals, loops, `select`,
  switch expression, interpolation) — **no user functions**; the second
  VM-execution benchmark baseline via the workflow, checked against the two-axis
  gate.
- **The §3.1.1 invariant, extended.** Every identifier node introduced across
  A–F (including the synthetic loop bindings C generates) carries a non-null
  `ResolvedType` and a non-null `Declaration` after type-check
  (`UnresolvedDecl.Instance` at error paths, D-311; `GrobType.Error` on the type
  side).
- **The Sprint 1–3 surface, lightly** — Sprint 4 must not have regressed it.
  `print(2 + 3 * 4)` → `14`, `grob run hello.grob` (Sprint 3's gate), the
  parser-recovery diagnostic counts.

### Explicitly OUT of scope (do not test, do not flag as missing)

- **Functions, lambdas, call frames, closures, upvalues** — **Sprint 5.** The
  calculator close-gate uses **no** user functions by design; a finding "the
  calculator should use functions" or "functions are not implemented" is noise.
  (`print`/`input` are built-ins, not user functions.)
- **Flow-sensitive narrowing** — `if (x != nil) { … x … }` narrowing `x` from
  `T?` to `T` is **Sprint 5.** A finding "the `if` body does not narrow the
  nullable" is noise; narrowing does not exist yet by design.
- **User types, structs, the `type` keyword** — **Sprint 6.** The calculator must
  not use a struct; if it does, _that_ is a finding (it tests a later sprint
  early).
- **Pattern matching beyond the D-277 grammar** (value, relational, catch-all).
  Richer patterns are not a v1 feature; their absence is not a finding.
- **An exhaustiveness check on `select`.** `select` is **non-exhaustive by
  design** (D-301); a finding "`select` does not error on a missing case" misreads
  the split. Exhaustiveness applies only to the switch **expression** (E).
- **A `for`/logical opcode.** There is no `for` opcode (`for...in` is a `while`
  lowering) and `&&`/`||` are jump-based, not opcodes. "There should be a `For`
  opcode" or "a `LogicalAnd` opcode" misreads §3.3.
- **The full benchmark suite / stability calibration** — Sprint 8. Sprint 4 adds
  the second VM-execution baseline only. "The regression gate is not calibrated"
  is out of scope; confirm against the benchmarking doc.
- **Per-error-code long-form docs** (`docs/errors/Exxxx.md`). Scheduled before
  v1 release; absence is not a Sprint 4 finding.

A finding of the form "there is no pattern-matching on types" or "functions are
not implemented" is noise. Discard it.

---

## 4. The Sprint 4 invariants — highest-value targets

These are the non-deferrable acceptance criteria and the structural pieces later
sprints build on. Confirm the exact wording in the spec before asserting. Prefer
a written assertion or a test — and for bytecode shape, a compile-and-disassemble
— over an eyeball read for every one.

### 4.1 The §3.1.1 invariant holds across every new identifier node

Walk a parsed-and-type-checked AST for a source file using every Sprint 4 form —
conditionals, `while`, all four `for...in` forms (including the synthetic counter
/ keys / iteration bindings the lowering generates), `select`, the switch
expression — and assert **every identifier node carries a non-null `ResolvedType`
and a non-null `Declaration`**. Per D-311 the declaration side uses the
`UnresolvedDecl.Instance` singleton at error paths, not literal `null`; assert it
by reference (`Assert.Same`). A genuinely null/unset `Declaration` or
`ResolvedType` — including on a synthetic loop binding — is a CORRECTNESS finding.

### 4.2 Jump and loop offsets are exactly right

An off-by-one in a jump or loop offset corrupts control flow without always
producing a wrong _value_ on a given input. Verify empirically, by
compile-and-disassemble:

- `if`/`else if`/`else`: the `JumpIfFalse` over each then-block and the
  unconditional `Jump` past `else` land on the intended instructions; an
  `else if` chain's exit jumps all backpatch to the same end-of-statement target.
- `while`: the exit `JumpIfFalse` and the backward `Loop` land correctly — no
  iteration too many or too few. Confirm with VM iteration counts, not only a
  final value.
- `&&`/`||`: the short-circuit jump shape, with the operator table's exact stack
  discipline (whether it leaves the left operand or pushes `false`/`true`), and
  **no** dedicated opcode.
- ternary and switch expression: exactly one arm reachable per condition/match,
  and every path leaves exactly one value on the stack (assert stack height).

### 4.3 `break`/`continue` resolve to the innermost loop, and `select` passes through

The loop-context stack (Increment B) is the foundation C and D build on. Verify:

- A nested `while`/`for`: `break`/`continue` act on the **innermost** loop; the
  outer loop is untouched.
- `break`/`continue` outside any loop are E2211/E2212, with source locations.
- **`select` pushes no loop context:** a `break`/`continue` inside a `select`
  case acts on the **enclosing loop** (compile `while { select { case … { break } } }`
  and confirm the `break` exits the `while`), or is E2211/E2212 if there is no
  enclosing loop. A `select` that captures `break`/`continue` itself is a
  CORRECTNESS finding.

### 4.4 `for...in` lowers correctly for every form

There is no `for` opcode; each form is a `while` lowering. Verify by
disassembly and by VM iteration:

- **Array single / index:** iterate every element in order; the index form
  exposes a correct zero-based `int` counter.
- **Map `(k, v)`:** the keys array is materialised **once** before the loop, in
  insertion order; `v` equals `m[k]` each iteration. Single-identifier `for k in m`
  is **E0502** suggesting `.keys`.
- **Range:** `0..3` yields 0,1,2,3 (inclusive); `0..10 step 5` yields 0,5,10;
  `3..0 step -1` yields 3,2,1,0. Descending **without** a negative `step` is
  **E0503**. The comparison opcode must agree with the step direction (`<=`
  ascending, `>=` descending) — a mismatch produces an infinite or zero-iteration
  loop.
- **`continue` targets the increment step**, not the condition — a `for` with a
  `continue` still advances and terminates. A hang here is a CORRECTNESS finding.
- The iterator variable is immutable in the body (**E0504** on reassignment); a
  non-iterable subject is **E0501**.

### 4.5 `select` is first-match, no fall-through, non-exhaustive

- The **first** matching case runs and the `select` exits; a later matching case
  does not also run (fall-through is a CORRECTNESS finding against §3).
- `default` runs only when no case matches; **no match and no `default` is a
  no-op, not an error** (D-301).
- A multi-value `case Y, Z` matches either value (two `Equal` tests ORed to one
  block).
- An incompatible case-value type is a compile error (E0001/E0002 per the
  registry).

### 4.6 The switch expression is exhaustive and value-leaving

- A non-exhaustive switch expression (no `_`, not full coverage) is a compile
  error (**E0505**). An exhaustive one (with `_`, or full enumerated coverage)
  type-checks. Confirm the exhaustiveness rule in §3.1 and throw adversarial
  inputs: a missing `_` over an open subject type must be E0505; a `_`-terminated
  switch must pass.
- All arms unify to one `ResolvedType` (the same unification as the ternary, §4.2);
  non-unifying arms are a compile error.
- Every compiled path leaves **exactly one** value on the stack — it is an
  expression usable in assignment and interpolation. A path that leaves zero or
  two values is a CORRECTNESS finding.

### 4.7 `ErrorCatalog` parity and no invented codes (D-308)

- `"E2211"`, `"E2212"`, `"E0501"`–`"E0505"` each appear **exactly once** in the
  solution (their descriptors) and are registered in `grob-error-codes.md` at the
  numbers the registry assigns (the next-free in their blocks). A duplicated
  literal, an unregistered code or a code raised by a string literal at a call
  site is a CORRECTNESS finding.
- No invented codes: grep for `"E0`, `"E1`, `"E2`, `"E5` literals outside the
  catalog; any hit is a finding. Every code a Sprint 4 diagnostic raises exists
  in the registry.

### 4.8 The close-gate and the negative paths

- `grob run calculator.grob` through the real CLI produces the expected stdout
  over the **Sprint 1–4 surface, with no user functions**. A wrong answer is the
  highest-priority CORRECTNESS finding. Confirm the script stays within the
  surface — if it uses a function, a struct or a stdlib module beyond the
  built-ins, that is itself a finding (the gate tests a later sprint early).
- The inverse, per new diagnostic class: a non-iterable `for...in` subject
  (E0501), single-ident map iteration (E0502), descending-without-step (E0503),
  iterator reassignment (E0504), a non-exhaustive switch (E0505) and
  `break`/`continue` outside a loop (E2211/E2212) each produce a compile-time
  diagnostic with a line number and **do not execute**.

### 4.9 The VM-execution benchmark baseline came from the workflow (D-309/D-313)

Sprint 4 lays the **second** VM-execution baseline. Confirm it was produced via
the `benchmark.yml` workflow on the canonical runner — not a local `dotnet run -c
Release` — and that `Grob.BenchCheck` ran the two-axis comparison (5% per-sprint
against the rolling baseline, 12% cumulative against the frozen origin, D-313). A
locally produced committed baseline, or one on a different runner type, is a
CORRECTNESS finding (it voids cross-run comparison). Gate calibration is out of
scope; baseline provenance is in.

---

## 5. The sprint-specific corrections to verify, not re-litigate

- **`select` is non-exhaustive; the switch expression is exhaustive (D-301).**
  This is a designed split. Verify the code matches each side — a non-exhaustive
  `select` is correct, a non-exhaustive switch expression is E0505. Do not argue
  one should behave like the other; if you think the split is wrong, that is one
  SEMANTIC line.
- **The calculator close-gate uses no user functions.** The Sprint 3 QA brief
  said the calculator "exercises `while` loops and functions" — that aside is
  **superseded**. For Sprint 4 the calculator is loop-and-`select` based over the
  Sprint 1–4 surface. A finding "the calculator should call a function" tests
  Sprint 5 early and is noise.
- **E2211/E2212/E0501–E0505 were assigned this sprint.** They are new registry
  entries at the next-free numbers in their blocks (ADR-0014, ADR-0017). Verify
  they exist, are referenced via catalog descriptors and carry the §-wording. "These
  codes did not exist before" is expected — they are new; the question is whether
  they are now correctly registered and used.

A "this `:=` could be a collection expression" or "this method could be a primary
constructor" suggestion is stylistic modernisation and out of scope. The build is
the detector for real C# 14 / .NET 10 defects (D-310): build clean in Debug and
Release and read the warning list.

---

## 6. The bisection tools still apply

The disassembler (Sprint 2 A) is the localisation tool for this sprint's bytecode
work. Where an end-to-end script gives a wrong answer, compile the fixture,
disassemble the `Chunk` and read it: the `if`/`else` jump targets, the `while`
exit and `Loop` offsets, the `&&`/`||` short-circuit shape, the `for...in`
lowering (synthetic slots, keys-array materialisation, comparison opcode, the
`continue` target), the `select` equality ladder and the switch-expression
value-leaving paths are all visible there. Prefer fixture-and-disassemble over
inspection for every bytecode-shape claim in §4 — the compiler is where the bugs
live, and the disassembler exists to make compiler-side faults unambiguous.

Feed the disassembler the loop and jump operands too (a `Loop` backward offset, a
`JumpIfFalse` forward offset, a `for...in` counter slot) and confirm the listing
resolves them readably; a disassembler that prints the loop operands incorrectly
is a CORRECTNESS finding, because it defeats the bisection it exists for.

---

## 7. Things that may look wrong but are likely deliberate

Before filing any of these as CORRECTNESS, find the governing `D-###`. If the log
confirms the behaviour it is not a finding; if the code contradicts the log,
_that_ is the finding.

- **`select` is non-exhaustive; no match + no `default` does nothing (D-301).**
  Not a finding.
- **The switch expression is exhaustive (E0505).** The asymmetry with `select` is
  the design.
- **`&&`/`||` are jump-based, not opcodes;** `for...in` is a `while` lowering with
  no `for` opcode. The closed enum carrying only `Jump`/`JumpIfFalse`/`JumpIfTrue`/
  `Loop` for control flow is correct.
- **The numeric range is inclusive both bounds;** `0..3` is 0,1,2,3. Descending
  needs an explicit negative `step` (E0503) — `3..0` alone is an error, not a
  silent empty or reversed loop.
- **The iterator variable is immutable in the loop body (E0504).** A finding "I
  can't reassign the loop variable" misreads §5.
- **The calculator uses no user functions.** Sprint 4 closes on a loop-and-`select`
  calculator; functions are Sprint 5.
- **No flow narrowing inside `if (x != nil)`.** Sprint 5. Not a finding.
- **The `Declaration` invariant uses `UnresolvedDecl.Instance`, not `null`
  (D-311);** the type side uses `GrobType.Error`. Assert by reference.
- **No new `src/` project, no new opcodes, no parser/AST edits.** Sprint 4 is
  type-checker, emission and VM-arm work over Sprint 1's grammar-complete AST and
  Sprint 2's closed enum, reusing Sprint 3 D's `emitJump`/`patchJump`.
- **The harness is Claude Code now (D-314).** `CLAUDE.md`, `.claude/commands/`,
  the `.claude/agents/` lowering subagent and the retired `.github/` Copilot
  harness under `prompts/` are methodology, not language surface. Not a finding.

The rule throughout: the decisions log is the authority. "This language should do
X differently" is a SEMANTIC observation at most — one line, no patch — and if the
log settles it, it is nothing.

---

## 8. How to run the work

1. Check out the merged Sprint 4 branch. Build in Debug and Release
   (`dotnet build`, `dotnet build -c Release`); note warnings, prioritise only
   those indicating a real defect (the C# 14 / .NET 10 check, §5).
2. Run the test suite (`dotnet test`). Report any failure verbatim — a failing
   test in delivered code is a high-priority CORRECTNESS finding.
3. `grob run` against fixtures: the close-gate `calculator.grob` (expect the §5
   stdout); one fixture per new diagnostic (E0501–E0505, E2211/E2212) expecting a
   stderr diagnostic, non-zero exit and no execution; `hello.grob` (Sprint 3's
   gate, expect no regression).
4. Compile-and-disassemble fixtures per §6 for every bytecode-shape claim in §4:
   the `if`/`else` jump targets, the `while` exit/`Loop` offsets, the `&&`/`||`
   shape, each `for...in` lowering (slots, keys materialisation, comparison
   opcode, `continue` target), the `select` ladder, the switch-expression
   value-leaving paths.
5. Drive nested-loop and `select`-pass-through cases through the VM with assertions
   (§4.3): innermost-loop resolution, `break`/`continue` inside a `select` acting
   on the enclosing loop.
6. Run the VM-execution benchmark category and confirm its baseline came from the
   workflow and passed the two-axis gate (§4.9).
7. Read the type checker and compiler cold against §1–§5 of the fundamentals.
   Write your own adversarial inputs per §4 — prefer adding throwaway xUnit tests
   in the existing projects (routed per §3.5) over scratch harnesses, so
   assertions are reproducible.
8. Verify the §4 invariants with assertions, not inspection, wherever practical.
   The jump/loop offsets (§4.2), the `continue`-to-increment-step target (§4.4),
   the `select` pass-through (§4.3) and the switch-expression value-leaving (§4.6)
   only submit to empirical verification.

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
"§4.4 `for...in` lowering: verified correct, no findings" is useful signal. Where
you relied on a spec file that was missing or differently located than §0
expected, say so at the top.

Do not fix anything in the SEMANTIC bucket. Do not redesign. Find where the code
lies to the spec — as the spec actually reads in the repo, not as this brief
paraphrases it — and tell me precisely where.
