# Increment prompt — crash oracle

**Authority: D-451.** It authorises this increment and records Chris's three rulings on
its scope. Read it in `docs/design/grob-decisions-log.md`, with D-353 (the adversarial
testing strategy), D-039 (the two-mode error strategy), D-300 (the error-recovering
parser), D-308 (the error catalogue), D-311 (the §3.1.1 sentinels), D-335 (solution
membership), D-420 (the Deferred Work Register and its stopping rule), D-428 (the
error-examples harness) and D-430 (the public samples gate). Before starting, read R-47
and R-57 to R-64 in the register in `docs/design/grob-open-questions.md`, and E9001
in `docs/design/grob-error-codes.md`. Where prompt and decision differ, the decision wins
and the difference is a finding to report.

**When it runs.** After PRs #262, #263 and #264 merge, and before the 9C `fs` increment
(D-451 Decision 3).

**Where it sits.** D-353 authorises `grob-adversarial-testing-strategy.md`, a pre-alpha
adversarial suite in seven pillars. This increment is the first slice of its Pillar 1,
compiler fuzzing, brought forward from the strategy's §11.1 (D-451 Decision 1). Read the
strategy's §1 (the contract and the exit-code set), §2 (the severity ladder and
deduplication), §3 (Pillar 1) and §10 (the `Grob.Torture` harness) before the gate. Its
names, its contract and its severity vocabulary hold here; where this prompt departs from
it, D-451 records the departure.

**Why this increment exists.** PR #262 and its two follow-ups found seven defects by
probing near the code they changed: R-57 to R-63. R-58, R-59, R-60 and R-63 reach the VM's
internal error; R-61 crashed the compiler with an unhandled .NET exception; R-57 runs a
wrong call and prints a wrong result; R-62 refuses a type it accepts elsewhere. R-58,
R-59 and R-61 were on `main` before the PRs; #263 closed R-59 and #264 closed R-61. Most are the type checker
accepting a program the compiler or the VM cannot run, and that class can be found
mechanically: **no program text, valid or not, may make Grob raise an internal error
or throw a host exception.** A program either gets diagnostics with registered codes, or
compiles and runs, with any failure a Grob runtime error. This increment builds the harness
that checks that at scale, runs a first campaign and triages what it finds.

**This increment builds an oracle; it does not fix what the oracle finds.** D-420's stopping
rule applies: a finding goes to the register with a minimal reproducer, unless it blocks the
harness itself. The one fix it makes is to the CLI, which reports such a failure as
E9001 in place of a .NET stack trace (D-451 Decision 2) and exits with the documented
codes (D-451 Decision 5, closing R-64).

**Model and effort, by session.**

| Session | Work | Model | Effort |
|---|---|---|---|
| A | §1 investigation gate | Opus | high |
| B | Change groups 1 and 2 — the oracle and the CI slice | Opus | high |
| C | Change groups 3 and 4 — the CLI's E9001 report and exit codes; the mutator | Sonnet | high |
| D | Change group 5 — the generator | Opus | high |
| E | Change groups 6 and 7 — the first campaign and triage; record | Opus | high |

The generator in group 5 is the one named sub-problem where a Fable subagent may be used,
and only if producing well-typed programs across the v1 surface proves fiddly in session D.
No other session uses Fable.

A session may cover fewer groups than its row; it may not cover groups from two rows.

**Session-start check — before anything else in every session:**

1. Read the branch's handoff file under `.grob-session/handoffs/` (absent in session A) and
   `git log` on the branch, and state which session row this is and which change groups it
   will cover.
2. State the model you are running as, from your own system context. If it does not match
   the row, **stop** and say so.
3. You cannot see the effort setting. State the row's expected effort and **wait for a
   one-word confirmation** before proceeding.

**Branch:** one concern, one branch. The concern is the crash oracle.

Every change group in §2 ends as its own commit with the full suite green, and `/handoff`
is run after each. A new session resumes from the handoff file and `git log`, never from
memory of a previous conversation.

---

## 1. Read-only investigation gate

*Session A — Opus, high.*

**No source edits until this gate is complete and reported.** Building, running the CLI and
running the suite are expected. Write the report to a **scratch file outside the working
tree**. In chat give the path and any STOP conditions that fired. End session A with
`/handoff`, naming the report's path.

State what you expect before each measurement, then measure, then explain every difference.

### 1.1 Define the failure precisely

List every way a run can end today, from source text to exit code, and classify each as
**allowed** or a **finding**. Confirm or correct this starting list against the code:

- Allowed: diagnostics, each with a code registered in `ErrorCatalog`, a line and a column
  inside the source; a clean exit; `exit(n)` with `n` in `0..255`; a Grob runtime error
  reported through `DiagnosticFormatter.WriteRuntime`.
- Finding: an `exit(n)` with `n` outside `0..255`, a P1 under the strategy's §1, which
  leaves open whether the runtime clamps or diagnoses it (record it, do not choose); a
  `GrobInternalException` from any phase; any other host exception from the
  lexer, parser, checker, compiler or VM; a diagnostic with no code, or with a location
  outside the source; an identifier left without a `ResolvedType` or a `Declaration` after
  checking (§3.1.1, D-311); a run that does not finish within the budget §1.5 sets.

`RunCommand` catches `GrobInternalException` around the VM only. The compile phase has no
handler, so R-61's crash printed a .NET stack trace, and neither phase catches any other
host exception. `RunCommand`'s comment cites D-039 for
not exposing a host stack trace, but D-039 is the two-mode error strategy and says nothing
about internal errors: report the citation as a finding. `WriteInternalError` is excluded
from coverage as unreachable, which R-58, R-59 and R-60 disprove. E9001, *internal
compiler error — please report*, is registered and is raised nowhere. Report every phase's
handling as it is, in `grob run`, in the REPL (`ReplCommand`) and in any other command
that runs the pipeline.

### 1.2 Design the E9001 report

D-451 Decision 2 rules that the CLI reports an internal error or a host exception from
any phase as E9001, with the bug-report line and no host stack trace. Propose, for change
group 3:

- **Where the handler sits.** `src/CLAUDE.md` allows catching `Exception` only at the
  outermost CLI entry point. Name the one place in each command that wraps the whole
  pipeline, from lexing to the end of the run, and say why it is outermost.
- **What it does not catch.** `GrobExitException` and `GrobRuntimeException` keep their
  paths. Say whether `OutOfMemoryException`, `StackOverflowException` or a cancellation
  can reach the handler, and what it does with each.
- **What it prints.** The E9001 title from `ErrorCatalog`, the phase, the exception's
  message, the bug-report line and no stack trace, all on stderr; the exit code, taken
  from the documented set (requirements §8, the strategy's §1), with the reason for it.
  Show the exact text for one compile-phase and one VM-phase failure.
- **The exit codes (R-64, D-451 Decision 5).** A compile-time diagnostic exits `2` and a
  usage error `3`, where both exit `1` today. List every path that returns an exit code
  in `CommandLineRunner`, `RunCommand` and `ReplCommand`, with its code today and under
  the documented set. Say which side an unreadable file falls on (the strategy's §1 names
  a missing file as a usage error) and what the REPL returns. List every test that
  asserts an exit code, saying which change and which stay, and every document that
  states one.
- **How it is tested** without an open defect's reproducer, which stops reproducing when
  its row closes: name the seam that makes a phase throw on demand, and say whether it
  needs a constructor or an `internal` hook on `RunCommand` and `ReplCommand`.
- **The coverage exclusion.** `WriteInternalError`'s `[ExcludeFromCodeCoverage]` is
  removed, since the path is now tested.

### 1.3 Inventory the seed corpus

Count, by location, every `.grob` source the mutator could start from:
`tests/fixtures/**`, `docs/errors/examples/**`, the validation scripts, the `grob` blocks
the public samples gate extracts (reuse its extractor, do not write a second one) and the
benchmark fixtures. Say whether the Grob sources held in C# raw string literals in the test
projects can be extracted reliably, and how many there are. A seed that does not compile
today is still a seed: the oracle checks invalid programs too.

### 1.4 Design the harness

Propose, with the alternatives considered and why they lost:

- **Where it lives.** In `tooling/Grob.Torture`, the harness D-353's strategy names
  (§10), with the CI slice in `tests/Grob.Torture.Tests` under the D-335 membership gate.
  The oracle is the in-process mode: it drives lexer, parser, checker, compiler and VM
  directly, which the strategy reserves for SharpFuzz, because only in process can it
  check the §3.1.1 invariant and supply fake capabilities. It must not be referenced by
  anything under `src/`, and no project under `src/` gains a reference. Name the projects
  it references.
- **Black-box mode.** The strategy's primary mode runs the built CLI as a process, with a
  timeout and the no-stack-trace assertion over stderr. Say whether this increment builds
  it, as the long mode's way of bounding a run (below) and as a check of change group 3's
  E9001 report, or leaves it to the Sprint 9/10 interlude, with the cost of each.
- **Isolation from the machine.** A mutated seed can call `fs`, `process`, `env` or the
  clock. The oracle runs every program against fake capabilities from `Grob.Runtime`'s
  ambient interfaces and never touches the real file system, environment, network or
  processes. Show how `PluginRegistration.BuildCatalogue` is given fakes.
- **Bounding a run.** Every run is bounded, in the CI slice and the long mode alike: a
  mutation can delete a loop's update, and a run that never ends blocks `dotnet test`.
  The VM dispatch loop is a hot path guarded by the benchmark gate and must not gain a
  per-instruction check. It needs none: `VirtualMachine.Run` takes a `CancellationToken`
  and checks it once per budget of steps, a check the code attributes to D-319's
  playground host. Propose the bound: a token cancelled after a timeout per run is the
  first candidate. Say how a cancelled run is classified (a finding, per §1.1), how the
  lexer, parser, checker and compiler are bounded, since the token reaches only the VM,
  and what each option costs.
- **Determinism.** Every program is reproducible from a seed. A finding is reported with
  its seed, its source and a minimised reproducer.
- **Minimisation.** A delta-debugging pass over lines, then over AST node spans, that keeps
  the failure signature while shrinking the source.
- **Signatures and known failures.** A finding raised as an exception has a signature of
  the phase, the exception type and the message with values removed, which refines the
  strategy's §2 deduplication (the normalised first stack frame); say whether the frame is
  added. §1.1's other findings raise no exception, so define a signature for each: an
  `exit(n)` out of range, a diagnostic with no code or a location outside the source, an
  identifier missing its `ResolvedType` or `Declaration`, and a run past its bound. For
  each, name the fields it uses, so that it can be deduplicated, minimised and matched to
  a register row as an exception's signature is. A finding is preserved in
  the strategy's `findings/` layout, named by its signature. A known-signature list keeps
  the CI slice green while a row is open. **A signature does not identify a root cause**:
  R-58 and R-63 both stop with `GrobValue kind mismatch: expected Float, actual Int`. So
  an entry names its register row and the programs it covers, by seed and mutation, and
  the slice suppresses only those programs; a new program with a known signature is a
  finding until it is triaged. A row that closes takes its entries with it.

### 1.5 Plan the budget

Measure programs per second through the pipeline on this machine. Propose the CI slice's
size so it adds no more than 30 seconds to `dotnet test`, and the long mode's size for a
campaign. The long mode is run by hand, never on a schedule (D-451 Decision 4): give the
command a maintainer runs and how long it takes.

### 1.6 Plan the mutation operators

The strategy's §3.1 mutates bytes and tokens, and its §3.2 generates pathological
programs; both aim at the lexer and the parser. This increment's operators are semantic
instead: they aim at the checker, where R-57 to R-63 were. There is no source printer: the
formatter is Sprint 12. Mutations edit source text at the
`Range` of AST nodes the parser returns. Propose the operator list, starting from these,
each aimed at a shape R-58 to R-61 took:

1. Remove a type annotation from a binding, parameter or field.
2. Replace a literal with one of another type, or with `nil`.
3. Make a declared type nullable.
4. Move an operand into an inferred lambda's parameter: `a + b` becomes
   `((v) => v + b)(a)`.
5. Swap a binary operator's operands.
6. Replace an identifier with another in scope.
7. Route a value through an array element, an anonymous struct field or a map value and
   read it back, so its type becomes unknown.
8. Swap `?.` and `.`; remove a nil-check guard.
9. Delete a statement, a `return` included.
10. Add, remove or name an argument.

---

## 2. Scope

Seven change groups, each a green commit. Land them in this order.

1. *Session B — Opus, high.* **The oracle.** Runs one source text through the pipeline in
   process, against fake capabilities, and classifies the result per §1.1. Tests prove each
   finding class is caught, using small hand-written sources and one known reproducer from
   R-58 and from R-60.
2. *Session B — Opus, high.* **The CI slice.** A deterministic run over the seed corpus
   unmutated, inside §1.5's budget, as a test in the oracle's test project. The
   known-signature list starts with the open rows the slice hits. Mutants join the slice
   in group 4, once the mutator exists.
3. *Session C — Sonnet, high.* **The CLI's E9001 report**, as §1.2 designs it, in every
   command that runs the pipeline. `RunCommand`'s D-039 citation is corrected and
   `WriteInternalError`'s coverage exclusion removed. Integration tests show a
   compile-phase and a VM-phase failure each print E9001 with the bug-report line, no
   stack trace and the exit code §1.2 chose, and that a Grob runtime error and `exit(n)`
   are unchanged. The CLI's exit codes become the documented set, closing R-64: a
   compile-time diagnostic exits `2` and a usage error `3`. Every test §1.2 lists changes
   with it, and every document that states the old codes is corrected.
4. *Session C — Sonnet, high.* **The mutator**, with §1.6's operators and the minimiser.
   Each operator has a test showing the edit it makes on a small source. The CI slice
   gains a fixed-seed sample of mutants, still inside §1.5's budget, and the
   known-signature list gains entries for the open rows those mutants hit.
5. *Session D — Opus, high.* **The generator.** A depth-bounded, type-directed generator of
   programs over the v1 surface: bindings, arithmetic, strings, arrays, maps, structs,
   anonymous structs, lambdas, nullables, `if`, `while`, `for...in`, `select`, the switch
   expression and `try`/`catch`. Generated programs are meant to type-check; one that does
   not is reported by the generator's own test, not by the oracle. R-40's benchmark
   generator is a separate concern and is not built here.
6. *Session E — Opus, high.* **The first campaign.** The long mode run at §1.5's size over
   the mutator and the generator. Every distinct new signature is minimised and filed in
   the register, one row per root cause, each with its reproducer and the measured output.
   Known rows are matched by minimised reproducer, not by signature alone, and are not
   refiled.
7. *Session E — Opus, high.* **Record.** `docs/dev/` gains a page on running the oracle by
   hand, reading a finding and adding a known signature. The landing entry.

### Out of scope — report, do not fix

- **Any defect the oracle finds**, beyond one that stops the harness running. It goes to
  the register (D-420). The E9001 report changes how a defect is shown, not whether it
  happens.
- **A new error code, opcode, token, AST node or grammar production.** E9001 is already
  registered.
- **Any change to the VM dispatch loop.**
- **A scheduled CI workflow** for the long mode (D-451 Decision 4). The Sprint 9/10
  hardening interlude revisits it.
- **The rest of the strategy's Pillar 1**: §3.1's byte and token mutation, §3.2's
  pathological generation and the parser depth guard it expects to force, and §3.3's
  SharpFuzz campaigns. They stay with the interlude (strategy §11).
- **Mutation testing of the C# test suite** (Stryker.NET). That belongs to the Sprint 9/10
  hardening interlude, with the adversarial review.

---

## 3. Implementation notes

**The oracle is a test of the checker.** Every finding means the checker accepted a program
it should have refused, or a later phase lost an invariant. Report it as that, with the
phase and the source, not as a VM bug.

**Never trust a green campaign.** A campaign that finds nothing proves only that its
operators found nothing. Before the first campaign, check the oracle finds R-58 and R-60
from their seeds through the mutator, not only from hand-written reproducers.

**Seeds stay fixed.** A CI slice that changes its programs between runs is flaky by design.
The long mode may draw fresh seeds; the CI slice may not.

**Complexity.** Extract guard-clause helpers; do not suppress S3776.

---

## 4. Tests

TDD throughout — red, green, refactor. Never merge known-wrong code. Quarantine with a
documented reason before weakening a test.

Required:

- Each finding class in §1.1 is caught: an internal error, a host exception in the
  compile phase and in the VM, a diagnostic outside the source, a missing `ResolvedType`
  and a run past the budget, each with the signature §1.4 defines for it; two programs
  with the same root cause share a signature, and two with different ones do not.
- Each allowed outcome passes: diagnostics, a clean exit, `exit(n)` and a Grob runtime
  error.
- The fake capabilities: a seed that writes, deletes and runs a process touches nothing on
  the machine.
- Each mutation operator's edit, and the minimiser keeping a signature while shrinking.
- The generator: a sample of generated programs type-checks.
- The CI slice is deterministic: two runs produce the same programs and the same results.
- The known-signature list: an entry without a register row fails the slice, and a
  program the list does not name is reported even when its signature matches an entry.
- An `exit(n)` outside `0..255` is classified as a finding, and `exit(42)` as allowed.
- The exit codes, through `grob run` and `CommandLineRunner`: `0` for a clean run; `1` for
  a Grob runtime error; `2` for a type error and for a syntax error; `3` for an unknown
  command, a missing file path and a file that does not exist; `n` for `exit(n)`.
- The E9001 report: a host exception from the lexer, parser, checker, compiler and VM each
  reaches it in `grob run`, and at least one phase does in the REPL; a `GrobRuntimeException`
  and a `GrobExitException` do not.

**Mutation-verify** four guards. For each: remove it, **predict the exact failure
signature before running**, confirm it, restore.

1. The classification of a host exception from the compiler as a finding.
2. The fake file system's isolation.
3. The known-signature list's register-row check.
4. The E9001 handler around the compile phase in `RunCommand`.

---

## 5. Deliverables

- Seven change groups per §2, each its own green commit.
- The gate report's §1.1 table of outcomes, as found, and §1.2's design of the E9001
  report.
- The CI slice's runtime, measured on the machine and in CI.
- The first campaign's figures: programs run, by source and operator; findings, by
  signature; each new register row and each known row matched.
- Coverage on new code at or above 90% line and branch.
- Error-code count unchanged, from the live registry; D-316 gate green.
- R-64 moved to the register's closed section by the landing entry, citing the exit-code
  tests as its evidence.
- The benchmark gate's result, with no baseline updated.
- A landing entry in the decisions log, in three-location lockstep, recording what was
  built and every divergence from this prompt. **Take the D-number from the live tail of
  the merged log**, never from memory or this prompt.
- Push the branch and open the PR as `/close-session` describes. Chris merges.

---

## 6. STOP conditions

Stop and report, do not proceed:

- The session-start check finds the running model does not match the session's row.
- A Sonnet session meets a design question the gate report does not answer.
- The oracle cannot run a program without touching the real machine.
- Bounding a run needs a change to the VM dispatch loop.
- The CI slice cannot fit §1.5's budget.
- The harness needs a reference from any project under `src/`, or a new error code,
  opcode, token, AST node or grammar production.
- The first campaign finds more than twenty distinct new signatures. Report the list and
  wait: triage at that scale is Chris's call on priority.
- The E9001 handler cannot wrap the whole pipeline from one outermost point in a command,
  or catching there would hide a `GrobRuntimeException` or a `GrobExitException`.
