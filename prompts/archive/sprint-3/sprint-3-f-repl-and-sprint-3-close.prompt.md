---
name: "Sprint 3 · Increment F — REPL and Sprint Close"
description: "Build ReplCommand in Grob.Cli — G> prompt, auto-printed expression results, multi-line input, history — wire the grob run hello.grob close-gate, and add the first VM-execution benchmark baseline via the benchmark.yml workflow (D-309). Closes Sprint 3."
agent: "Grob Compiler Engineer"
tools: ["search", "read", "edit", "execute"]
---

# Sprint 3 · Increment F — REPL and Sprint Close

You are closing Sprint 3. Increments A–E delivered declarations and assignment
with the scope chain, `grob run`, `const`/`readonly`, the nullable runtime and
string interpolation. This increment adds the interactive REPL (`grob repl`),
wires the end-to-end smoke script that gates the sprint, and adds the first
VM-execution benchmark baseline now that running scripts is possible. The REPL
is the genuinely fiddly piece — multi-line input, persistent session scope,
auto-printed results — but it is REPL-local, not load-bearing for later sprints.

Read, in order:

1. `docs/design/grob-v1-requirements.md` §4 (Sprint 3 — the REPL bullets and the
   sprint acceptance), §1 (the smoke-test framing), §3.5 (test routing).
2. `docs/design/grob-solution-architecture.md` — `Grob.Cli` (`ReplCommand`,
   `WindowsTerminalProfile`, the error formatter) and the composition-root
   constraint.
3. `docs/design/grob-personality-identity.md` — the `G>` prompt, REPL tone,
   quiet-on-success, errors to stderr.
4. `docs/design/grob-tooling-strategy.md` — the REPL section, if it specifies
   multi-line continuation or history behaviour beyond §4.
5. `docs/design/grob-benchmarking-strategy.md` **§8** (the canonical production
   path) and the VM-execution category shape; `docs/design/grob-vm-architecture.md`
   for what a representative execution benchmark exercises.
6. Decision **D-309** (benchmark production via the `benchmark.yml` GitHub
   Actions workflow; `windows-latest` canonical runner) in the log.

> **Verify before relying on cited decisions.** Grep the log to confirm D-309
> (baselines produced via `benchmark.yml`, `workflow_dispatch`, `windows-latest`;
> the engineer commits the `-report-full.json` as the category baseline). If it
> has been superseded, surface it.
>
> **Benchmark production — inline reference (authoritative source is
> `grob-benchmarking-strategy.md` §8 / D-309; reproduced so the implementation
> does not depend on a fetch landing well).** The canonical path for a committed
> baseline is the `benchmark.yml` workflow, triggered manually
> (`workflow_dispatch`) on `windows-latest`. Local `dotnet run -c Release` is for
> debugging only and must **not** produce the committed baseline. Add the
> VM-execution benchmark category and its fixture; the actual baseline JSON is
> produced by triggering the workflow and committing the `-report-full.json`
> for the new category — record the run ID and runner in the commit message.
> All baselines of a category must use the same runner type; mixing voids the
> comparison.
>
> **The close-gate — inline reference.** Sprint 3's acceptance does **not**
> include the calculator smoke test — the calculator needs `while` loops
> (Sprint 4) and functions (Sprint 5). The Sprint 3 end-to-end gate is
> `grob run hello.grob` over a script exercising the Sprint 3 surface only:
> declarations, the scope chain, `const`/`readonly`, nil safety (`??`/`?.`) and
> string interpolation — no control flow, no function calls. Author `hello.grob`
> to that surface and assert its stdout end-to-end, the way Sprint 2 D closed on
> `print(2 + 3 * 4)` → `14`. The calculator end-to-end belongs to Sprint 4's
> close.
>
> **Sequencing note.** This is Increment F of the agreed breakdown: A → B → C →
> D → E → **F (REPL + close)**. The pipeline and CLI composition (`RunCommand`)
> exist from B; `ReplCommand` reuses the same composition pattern with a
> persistent session scope.

## Branching and commits

This work does **not** go on `main`. Before writing any code:

1. Run `/start-branch` and propose the branch name
   `feat/repl-and-sprint-3-close`.
2. Wait for Chris to agree the name and create the branch.
3. Implement the increment with TDD as `start-branch` describes.
4. When ready to commit, run `/commit-message` against the staged changes — it
   refuses to commit on `main` and will draft a conventional-commits message.
5. Stop after the commit lands locally. Pushing and opening the PR is Chris's
   action.

If you find yourself on `main` with edits, stop immediately and surface it.

## What is already done

- **Sprint 1 & 2:** front end and back end complete. `RunCommand` and the CLI
  composition root exist (Sprint 3 B). The benchmark skeleton and the first
  compile-time baseline exist (Sprint 2 D), produced via the `benchmark.yml`
  workflow (D-309). `ErrorCatalog` (D-308), C# 14 (D-310).
- **Sprint 3 A–E:** declarations and assignment with scope/slots; `grob run`;
  `const`/`readonly` (E0205/E0206 registered); nullable runtime (`T?`, `??`,
  `?.`, `IsNil`, the `emitJump`/`patchJump` helper); string interpolation
  (`BuildString`, E0102). The full Sprint 3 language surface compiles and runs.

## Deliverable for this increment

`ReplCommand` in `Grob.Cli`, the close-gate smoke script and test, plus the
VM-execution benchmark category and baseline.

1. **`ReplCommand`.** The `G>` prompt; read a line (or a multi-line block),
   compile and execute it against a **persistent session scope** that carries
   declarations across entries, and print results per the rule below. Errors to
   stderr; the REPL does not exit on a compile or runtime error — it reports and
   prompts again. Quiet on success otherwise.
2. **Expression results auto-printed.** A bare expression entry prints its value;
   a declaration or statement entry does not print (it declares/acts and
   prompts again). Decide the expression-vs-statement rule from the AST the
   parser already produces, and document it.
3. **Multi-line input.** When an entry is incomplete (an open `{ }` block or an
   otherwise unfinished construct), continue reading lines until the construct
   closes, then compile the whole. Use the parser/lexer's bracket-nesting signal
   rather than re-implementing brace counting where avoidable.
4. **History.** Readline-style history (up/down recall) or the platform
   equivalent named in the tooling strategy.
5. **Persistent session scope.** Declarations from earlier entries remain in
   scope for later entries within the session. Decide how the session scope maps
   onto the globals table the compiler/VM already use, and document it.
6. **Close-gate script and test.** Author `hello.grob` to the Sprint 3 surface
   (declarations, scope, `const`/`readonly`, `??`/`?.`, interpolation — no
   control flow, no functions) and assert `grob run hello.grob`'s stdout
   end-to-end in `Grob.Integration.Tests`.
7. **VM-execution benchmark.** Add a VM-execution benchmark category to
   `bench/Grob.Benchmarks` with a representative fixture (a declaration/
   arithmetic/interpolation script the VM now runs), and produce its baseline via
   the `benchmark.yml` workflow (D-309) — commit the `-report-full.json` for the
   new category, not a locally produced file.

## Out of scope

No control flow in the REPL beyond what the Sprint 3 surface supports — `if`/
`while`/`for` entries are Sprint 4. No functions, closures or `fn` entries —
Sprint 5. No `.repl` meta-commands, no syntax highlighting, no autocomplete
(later tooling). The calculator smoke test is **not** wired here — it is Sprint
4's gate. Do not edit the compiler, VM, parser or AST — `ReplCommand` consumes
their public surface; if the REPL exposes a pipeline bug, surface it rather than
patching the engine from the CLI. Do not run the benchmark baseline locally as
the committed artifact (D-309).

## Tests

Per §3.5:

- **Integration (`Grob.Integration.Tests`):**
  - `grob run hello.grob` produces the expected stdout and exit `0` — the
    close-gate.
  - The REPL, driven through `ReplCommand` with scripted input and captured
    output writers (not a live terminal): a bare expression prints its value; a
    declaration does not print; a later entry sees an earlier entry's
    declaration (persistent scope); a multi-line `{ }` block is read to
    completion before compiling; a compile error reports to stderr and the REPL
    continues to the next prompt.
- **Benchmark smoke:** the VM-execution category builds and runs via the
  documented workflow path; the baseline JSON for the new category is committed.

## Acceptance

- `dotnet build` and `dotnet test` green at the solution root.
- `grob repl` works: `G>` prompt, auto-printed expression results, multi-line
  input, history, persistent session scope, report-and-continue on error.
- `grob run hello.grob` executes the Sprint 3-surface script end-to-end — the
  close-gate is green.
- The VM-execution benchmark category exists and its baseline is committed via
  the `benchmark.yml` workflow.
- §4's whole-sprint acceptance is met: variables declared/reassigned/used;
  `const`/`readonly` semantics; nil safety; string interpolation; the REPL;
  `grob run`.
- The DAG holds; the compiler, VM, parser and AST are unchanged.

## Model

Sonnet 4.6 (High) throughout. The REPL is fiddly — multi-line completeness
detection, the expression-vs-statement print rule, persistent session scope —
but it is REPL-local: nothing in later sprints builds on its internal shape, so
it does not meet the Opus trigger ("a structural decision later sprints build
on"). Reach for Opus only if the persistent-session-scope mapping onto the
globals table and the multi-line completeness signal interact in a way that
needs a judgement call; with the parser's bracket-nesting signal and the
existing globals table to lean on, Sonnet handles it. The benchmark wiring is
mechanical against D-309.

## Hand-off

Summarise: the `ReplCommand` shape, the expression-vs-statement print rule, the
multi-line completeness mechanism, the persistent-session-scope mapping onto the
globals table, the `hello.grob` close-gate script and its asserted output, the
VM-execution benchmark category and how its baseline was produced (workflow run
ID and runner), and the test files added. Note that Sprint 3 is closeable on
this increment's green acceptance, and flag the deferred documentation-authority
call: whether to rewrite §4 to reflect the A–F structure. Next is Sprint 4 —
control flow — which reuses the `emitJump`/`patchJump` helper Increment D built.
