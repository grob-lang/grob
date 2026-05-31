# Grob — Sprint 2 QA Fix Session

> **For:** the standard working model (Sonnet 4.6 throughout — the design
> calls in this session were made before the prompt was written; no
> judgement points remain for the model except the Ubuntu/Windows runner,
> which is a maintainer call surfaced in §3.B).
> **Context:** An external cold-reader (GPT-5.3-Codex) ran an adversarial QA
> pass over the Sprint 2 back end and filed two CORRECTNESS findings plus
> clean verifications on the other ten invariants the brief listed. Those
> findings have been triaged. This session implements the verified fixes.
> Do not re-run the whole QA pass — that work is done.
> **The decisions log is the authority.** Where this brief cites a `D-###`
> or a spec section, that document governs; if the code or this brief
> disagrees with it, the decisions log wins — surface the conflict rather
> than guessing.

---

## 0. Ground rules for this session

- Full files, never patches, as deliverables — but you are editing a live
  repo, so make the edits in place and show the complete changed files in
  your summary.
- Every fix ships with a test. A fix with no regression test is not done.
- British English in comments and docs. No Oxford comma. Never the word
  "simply".
- After each fix, `dotnet build` then `dotnet test` must be green before
  moving on.
- Verify before editing: grep the pattern across the codebase before
  changing one site — most of these fixes touch more than one call site.
- One commit per fix where the structure allows it. The artifact cleanup,
  type-checker fix, benchmark baseline, strategy doc update, and each
  decisions-log entry are independent — keep them on separate commits so
  a future bisect lands cleanly.

---

## 1. Context — benchmarks moved to a manual Actions workflow

Between Sprint 2 implementation and the QA pass, the maintainer moved
benchmark execution from local invocation to a manually-triggered GitHub
Actions workflow at `.github/workflows/benchmark.yml`. The rationale: local
PC was tied up too long for consistent results, and a standardised CI
runner gives a more reliable baseline.

**This affects Codex's Finding 2 but does not invalidate it.** The
deliverable the spec requires (BenchmarkDotNet JSON at
`bench/Grob.Benchmarks/baseline/compile.json`, committed) is unchanged.
What changed is _how_ the JSON gets produced — on a CI runner, not your
local machine. The placeholder `compile.json` is still a placeholder; the
finding still stands; only the mechanism for filling it is different.

The workflow as written:

- Triggers on `workflow_dispatch` only (manual, no schedule, no PR trigger).
- Lets the operator pick Ubuntu or Windows runner via a dropdown.
- Restores, then runs `dotnet run -c Release --project bench/Grob.Benchmarks
-- --filter '*' --exporters json markdown`.
- Publishes the markdown reports to the job summary for inline viewing.
- Uploads the `BenchmarkDotNet.Artifacts/results/` directory as a workflow
  artifact, 90-day retention.

The corpus does not yet reflect this. Most of `grob-benchmarking-strategy.md`
still assumes local invocation as _the_ entry point. The strategy doc
needs a workflow-aware update (see §3.C). That update is documentation, not
design — the spec deliverables don't change, only the production mechanism
does.

---

## 2. Triage summary — what is real and what is not

| Finding                                                         | Codex bucket | Verdict   | Action     |
| --------------------------------------------------------------- | ------------ | --------- | ---------- |
| Undefined identifier leaves `Declaration` null after type-check | CORRECTNESS  | **Valid** | Fix — §3.A |
| Compile baseline JSON is still a placeholder                    | CORRECTNESS  | **Valid** | Fix — §3.B |

Plus three follow-ups that came out of this triage or the QA run itself,
none a Codex finding but all worth doing in this session:

- **Benchmark strategy doc** needs a section describing the Actions
  workflow as the canonical entry point — §3.C.
- **Local benchmark artifacts** from Codex's QA run are sitting in the
  working tree; need a `.gitignore` entry and a sweep — §4.
- **Two decisions-log entries** owed — §5. One for the benchmark workflow
  move, one for the C# 14 / .NET 10 pinning correction earlier this sprint
  (the QA pass verified Debug and Release builds clean).

§4.1 / §4.5 / §4.6 / §4.7 / §4.8 / §4.9 of the QA brief all verified clean.
Do not "tidy" them, do not refactor under their tests, and do not regress
their assertions in the course of making the fixes below.

---

## 3. Fixes

### §3.A — Fix 1: `Declaration` non-null on every identifier after type-check

#### What the spec requires

`grob-v1-requirements.md` §3.1.1, verbatim:

> Every identifier node in a type-checked AST carries a non-null
> `ResolvedType` and a non-null `Declaration`. This makes the constraint
> testable and prevents regression.

The shape declared in §3.1.1 has `public AstNode? Declaration { get; set; }`
— nullable in the _shape_ so Sprint 1 could compile without a type checker
existing yet, **not** nullable as a permitted post-type-check state. After
the type-check pass completes, `Declaration` must be non-null on every
identifier node, including identifiers that the checker failed to resolve.

D-137 is the governing decision; it establishes day-one LSP metadata so
later sprints (the LSP) can rely on it without retrofit.

#### What the code does now

`TypeChecker.Expressions.cs:48` (per the Codex finding): unresolved
identifiers emit `E1001`, set `ResolvedType = Error`, and explicitly leave
`Declaration = null`. The cascade-suppression half (`ResolvedType = Error`)
is correct — that is the §29.3 mechanism and the type registry confirms it
— but the `Declaration = null` half violates §3.1.1.

The Sprint 1 QA-fix predicted this exactly: it added a pending test for the
"`Declaration` non-null after type-check" invariant and noted that Sprint 2
must make it pass. That test is the one that is currently failing in spirit
(or hasn't been promoted from pending — either way, the invariant is not
yet met).

#### The fix — singleton "unresolved declaration" sentinel

The maintainer has decided: **use a singleton sentinel**, analogous to how
the `Error` type works on the type side. One shared `AstNode` instance
representing "unresolved declaration", returned at every site where the
type checker would otherwise have left `Declaration = null`. The rationale,
recorded here so the implementing chat has it without needing to ask:

- Symmetry with the §29.3 `Error` type: one shared cascade-suppression
  token on the type side, one shared sentinel on the declaration side. One
  mechanism, applied consistently to both halves of an unresolved
  identifier.
- The LSP go-to-definition argument for per-error synthetic nodes is not
  real: on an unresolved identifier the LSP should be surfacing the
  diagnostic, not navigating anywhere. Per-error nodes would solve a
  problem the design does not have.
- A singleton is allocation-free at the failure path — every unresolved
  identifier reuses the same instance.

Implementation:

1. **Define the sentinel.** Add an `UnresolvedDeclaration` singleton to
   the same module that hosts the `Error` type (or to wherever the type
   registry's existing sentinels live — read `grob-type-registry.md` and
   the type-registry source for the right home; do not invent a new
   location if an obvious one already exists). The sentinel is a real
   `AstNode` subclass (or a static instance of a small dedicated class)
   with a clearly-named identity — `UnresolvedDeclaration.Instance` or
   equivalent. It is internal to the compiler; nothing outside the type
   checker, the LSP integration, and any future code that explicitly
   handles unresolved declarations should care about it.
2. **Replace every `Declaration = null` site.** Grep the type-checker
   source for `Declaration = null`, `Declaration =`, and any constructor
   call to `IdentifierExpr` (or whatever the repo calls it) — there is
   likely more than one site. Set `Declaration = UnresolvedDeclaration.
Instance` at each error path. Confirm the full set before editing.
3. **Update the §3.1.1 invariant test** to cover the unresolved-identifier
   case explicitly: a tree containing one unresolved identifier still
   satisfies "every identifier node carries non-null `Declaration`" after
   type-check.
4. **Promote** the Sprint 1 QA-fix's pending / skipped test (if it is
   still marked pending) to an active assertion.

#### The tests

- A type-check of `x := missing + 1` (the Codex repro) walks the resulting
  AST and asserts every `IdentifierExpr` has non-null `Declaration` — and
  that the unresolved `missing` identifier specifically points to
  `UnresolvedDeclaration.Instance`. The diagnostic count for that input is
  exactly 1 (`E1001` at the `missing` site); no additional diagnostics,
  including not a derived one from `x`'s RHS being `Error`-typed.
- Cascade suppression applies to _derived_ type errors, not to E1001
  itself — a tree with `missing + missing + missing` produces three
  `E1001` diagnostics (one per unresolved reference), but no additional
  derived errors from the `Error`-typed sub-expressions. (This protects
  the §4.5 verification from regressing under the new `Declaration`
  assignment.) All three `missing` identifier nodes point to the same
  sentinel instance (reference equality, not just shape equality).
- The §3.1.1 verification test from Sprint 2 still passes — every
  identifier in a fully-resolved tree carries the _real_ declaration node,
  not the unresolved sentinel. The fix must not regress the success path
  into pointing every identifier at the sentinel.

#### Decisions-log entry

This is a new design choice worth recording. Call it the "unresolved-
identifier declaration sentinel" — an addendum to D-137 (which establishes
the non-null-after-type-check invariant for `Declaration`). Body should
cross-reference the §29.3 `Error` type and explicitly note the symmetry
between the two sentinels. Numbering: next free `D-###`.

Short prose entry; the implementation detail is in the type registry, the
decision is "we use a singleton, not per-error synthetic nodes, for these
reasons". Update the summary index in lockstep with the full entry.

---

### §3.B — Fix 2: Benchmark baseline JSON (produced via the Actions workflow)

#### What the spec requires (current corpus)

- `grob-v1-requirements.md` §4 acceptance: _"the first baseline JSON is
  committed."_
- `grob-benchmarking-strategy.md` §7.6: `compile.json` — _"BenchmarkDotNet
  JSON for compile-time category"_. Four such files, one per category.
- `grob-benchmarking-strategy.md` §11: _"The baseline JSON files are
  committed for the first time at the close of Sprint 2."_
- D-302 is the governing decision.

The deliverable is unchanged by the workflow move — JSON, BenchmarkDotNet-
exported, committed at `bench/Grob.Benchmarks/baseline/compile.json`. Only
the production mechanism is different.

#### What the code does now

`bench/Grob.Benchmarks/baseline/compile.json` is a placeholder — comment
plus empty `Benchmarks` array. The benchmark itself runs (Codex confirmed
end-to-end via the local invocation; the Actions workflow runs the same
project with `--exporters json markdown`). The gap is that the actual
JSON output has never been captured and committed.

#### The fix

The first baseline must come from a workflow run, not a local run — that
is the whole point of moving to CI for consistency. Procedure:

1. **Trigger the workflow.** On GitHub, run the `Benchmarks` Actions
   workflow manually (workflow_dispatch). The runner choice (Ubuntu vs
   Windows) is the maintainer's call — see the "Runner choice" sub-section
   below; do not pick unilaterally.
2. **Download the artifact.** The workflow uploads
   `BenchmarkDotNet.Artifacts/results/` as `benchmark-results-<runner>-
<run-id>`, 90-day retention. Download and extract.
3. **Identify the right JSON file.** BenchmarkDotNet produces two JSONs
   per benchmark class — `<class>-report-brief.json` (trimmed summary)
   and `<class>-report-full.json` (statistical detail plus
   `HostEnvironmentInfo`). **Use the full report.** The host-environment
   block is what makes the baseline meaningful for future regression
   comparison — it records the CPU, OS, runtime version and GC mode the
   baseline was measured under. Without it the baseline is just numbers
   in a vacuum.
4. **Replace the placeholder entirely.** Do **not** paste the `Benchmarks`
   array from the workflow output into the existing `compile.json`. The
   current file is a placeholder and is wrong. The committed baseline
   must be the _whole_ BenchmarkDotNet JSON: `Title`, `HostEnvironmentInfo`,
   `BenchmarkDotNetCaption`, `BenchmarkDotNetVersion`, then `Benchmarks`.
   Procedure: delete `bench/Grob.Benchmarks/baseline/compile.json`, copy
   the extracted `<class>-report-full.json` to that path, rename to
   `compile.json`.
5. **Verify** the committed JSON has the BenchmarkDotNet shape — `Title`
   present, `HostEnvironmentInfo` populated with CPU and runtime info,
   `Benchmarks` array non-empty with at least one entry containing
   statistical detail.
6. **Commit message** records: the workflow run ID, the runner used, and
   the Sprint 2 close context ("first compile-time baseline per D-302,
   refined production mechanism per D-### the workflow-move entry").
   This gives a future-grep anchor for "where did this baseline come
   from and why this runner".

**Runner choice.** Ubuntu vs Windows is not arbitrary. Grob is
Windows-primary (the user base is Windows developers and sysadmins per
the project's design corpus), so Windows runner is the closer match to
the user-facing performance reality. Ubuntu runners are slightly faster
and slightly more predictable in measurement noise on GitHub-hosted
infrastructure. The trade-off is real and the maintainer should pick
deliberately — there is no obviously-correct default. **Surface the
question and wait for the maintainer's call** before triggering the
workflow. Once chosen, record the choice in the decisions-log entry for
the workflow move (§5) so future runs use the same runner consistently
— mixing runners across baselines voids the comparison.

#### The tests

- A test (or a CI check, whichever the repo already supports) that asserts
  `bench/Grob.Benchmarks/baseline/compile.json` exists, is non-empty, and
  contains the BenchmarkDotNet shape (presence of `Title`,
  `HostEnvironmentInfo` and `Benchmarks` fields; `Benchmarks` array
  non-empty). This is the regression guard against the placeholder coming
  back.

#### Out of scope for this fix

- **Re-running the workflow to produce comparison data for the 5%
  regression gate (§6 of the benchmarking strategy).** That gate is a
  Sprint 3+ concern; Sprint 2 only owes the first baseline.
- **Automating "commit the baseline" as part of the workflow** (a
  workflow-opens-PR-with-updated-baseline shape). Worth discussing
  later, but the manual trigger + manual commit is fine for Sprint 2.

---

### §3.C — Fix 2 follow-on: Update the benchmarking strategy doc

The corpus assumes local invocation throughout `grob-benchmarking-
strategy.md`. After the workflow move, the doc needs a workflow-aware
update. **This is documentation, not design** — the spec deliverables
don't change, only the production mechanism does.

Edits required, all to `docs/design/grob-benchmarking-strategy.md`:

1. **A new section** describing the Actions workflow as the canonical
   entry point for producing baselines and for regression-check runs. Name
   the workflow (`benchmark.yml`), the trigger (manual / workflow_dispatch),
   the runner choice (and that it must be consistent across runs of the
   same baseline category — see the runner-choice rationale in §3.B), and
   the artifact-collection flow (download from the workflow run, extract,
   take the `-report-full.json`, commit it as the category baseline).
2. **Update existing references to local invocation** so they read as
   "supported for one-off debugging, but the canonical baseline production
   path is the workflow". Do not delete the local-invocation
   documentation outright — it remains useful for debugging a benchmark
   crash before triggering it on CI — but reframe it as the secondary
   path.
3. **Add a runner-choice note** to §6 (or wherever the 5% regression gate
   is described): a regression comparison only makes sense between runs on
   the same runner type. Cross-runner comparisons are noise.
4. **Update the footer changelog** in the doc with this session's edit,
   citing the decisions-log entry from §5 that authorises the workflow
   move.

Do not edit `grob-v1-requirements.md` §4. The acceptance text there ("the
first baseline JSON is committed") is still accurate.

Do not edit §7.6 or §11 of the benchmarking strategy. The baseline shape
(JSON, BenchmarkDotNet output, committed) is unchanged.

---

## 4. Local artifact cleanup from the QA run

Codex's QA pass ran the benchmark locally (per §8 step 3 of the QA brief —
"Run the benchmark skeleton end-to-end via the documented invocation").
That run produced a `BenchmarkDotNet.Artifacts/` directory at the **repo
root** (not under the benchmark project — BenchmarkDotNet writes artifacts
relative to the current working directory, which on `dotnet run` from
the repo root lands them at the root). The directory contains the
results/ subdirectory plus the CompileBenchmarks-report files. None of it
is tracked — VS Code shows the folder and contents as untracked (green
indicator) — so nothing has leaked into history yet.

With the workflow move, local benchmark runs are now the _exception_
(debugging, one-off baseline production for §3.B), not the canonical path.
Local artifacts must never be committed; only the workflow-produced JSON
in `bench/Grob.Benchmarks/baseline/` is tracked.

1. **Add the `.gitignore` entry first.** Edit the root `.gitignore` (not
   a project-level one — the artifacts land at the repo root, so the
   ignore rule belongs there). Add a single line:

    ```
    BenchmarkDotNet.Artifacts/
    ```

    No `!baseline/` exception is needed — `bench/Grob.Benchmarks/baseline/`
    lives at a different path and the two never overlap. Adding the
    exception would be cargo-culted and slightly confusing.

2. **Delete the existing folder.** `rm -rf BenchmarkDotNet.Artifacts/` at
   the repo root. (Order matters: gitignore first, delete second. If you
   delete first and forget the gitignore, the next local run puts the
   folder back and you are in the same state.)

3. **Verify** `git status` is clean. If anything is staged or tracked
   from the old folder, unstage and remove from the index — but the
   screenshot evidence is that nothing was tracked, so this is just a
   confirmation step.

4. **Confirm the gitignore works.** Run the benchmark once locally
   (`dotnet run -c Release --project bench/Grob.Benchmarks` from the
   repo root) — it will recreate `BenchmarkDotNet.Artifacts/`. Check
   `git status` again; the folder should not appear. Then delete it
   again so the working tree is clean for the rest of the session.

This is housekeeping, not a defect. Five-minute fix; do not over-engineer.

---

## 5. Decisions-log entries owed

Three entries this session, all confirmed.

### Entry 1 — Benchmark execution moved to Actions workflow

Captures:

- The benchmark execution mechanism moved from local invocation to a
  manually-triggered GitHub Actions workflow at
  `.github/workflows/benchmark.yml` between Sprint 2 implementation and
  Sprint 2 QA close.
- Rationale: local PC tied up for too long to produce consistent
  baselines; CI runner gives a standardised reference.
- The runner choice (Ubuntu or Windows, per §3.B) is the chosen default
  going forward — record which.
- The deliverables specified by D-302 (JSON baselines, committed) are
  unchanged; only the production mechanism is different. D-302 is **not**
  superseded — it is _refined_, and this new entry points back at it as a
  refinement, not a replacement.

Numbering: next free `D-###`. Short prose body. Cross-reference D-302 and
the workflow file. Update the summary index in lockstep and the footer
changelog.

### Entry 2 — C# 14 / .NET 10 pinning correction

The QA brief's §5 explicitly verified this is clean: _"Debug and Release
builds were green with no warnings"_, with `Directory.Build.props:7` and
`global.json:3` as the citation points. Record the correction in the log
so a future-grep finds the answer.

Not an ADR-worthy decision, not a design change. Session-log entry
capturing:

- The project pinned C# 13 while building against .NET 10 for part of
  Sprint 2.
- The pinning was corrected to C# 14 to match the SDK.
- A Sprint 2-end QA verified Debug and Release builds clean with no
  warnings under the corrected pinning.
- The corrected pinning is canonical going forward; future sprints write
  against C# 14.

Numbering: next free `D-###` after Entry 1. Short prose body. Cross-
reference `Directory.Build.props` and `global.json`. Update the summary
index in lockstep and the footer changelog.

### Entry 3 — Unresolved-identifier declaration sentinel

The §3.A design decision recorded as an addendum to D-137. Captures:

- The §3.1.1 invariant requires non-null `Declaration` on every identifier
  after type-check.
- For unresolved identifiers, the type checker assigns a singleton
  `UnresolvedDeclaration.Instance` sentinel, analogous to the §29.3
  `Error` type on the type side.
- Rationale: design symmetry with the existing cascade-suppression
  mechanism; allocation-free; LSP go-to-definition does not navigate
  unresolved identifiers, so per-error synthetic nodes solve no real
  problem.

Numbering: next free `D-###` after Entry 2. Short prose body. Cross-
reference D-137, §29.3, and the type registry. Update the summary index
in lockstep and the footer changelog.

---

## 6. Sequencing and model choice

Do the fixes in this order, building and testing between each. **The order
matters and is principled — do not reorder.** The committed baseline must
represent the state of the code at sprint close, _including_ the
type-checker fix; running the workflow before §3.A lands would commit a
baseline against pre-fix code, which is not what Sprint 2 close means.
Hence §3.A precedes §3.B.

1. **§4** (artifact cleanup) first. Trivial, clears noise, lets later
   steps work in a clean tree. Sets up the `.gitignore` before any
   benchmark run produces output that might leak into the next commit.
2. **§3.A** (the type-checker fix). The singleton sentinel implementation
   per the inline decision. Land Entry 3 (decisions-log) with this fix.
3. **§3.B** (the benchmark baseline). Surface runner choice (Ubuntu vs
   Windows) to the maintainer, wait for the choice, trigger the workflow
   _against the fixed code_, download artifact, commit the full report
   JSON as `compile.json`.
4. **§3.C** (the strategy doc update). Once the workflow run has happened
   and the runner choice is recorded, update the doc with the new
   canonical entry point.
5. **§5 Entry 1** (workflow move) — lands after §3.B and §3.C so the
   entry can cite both the workflow and the doc edit. Standalone commit.
6. **§5 Entry 2** (pinning correction) — its own commit. Doesn't depend
   on anything else; lands here to keep one-commit-per-decision.

Model: Sonnet 4.6 throughout. The two design judgement points — Option 1
vs Option 2 in §3.A and the rationale for the strategy doc shape in §3.C
— were decided before this prompt was written; the only maintainer call
remaining is Ubuntu vs Windows in §3.B, and that's a configuration
choice, not a design one. Surface it and wait, but do not stop on it for
long — the rest of the sequence is mechanical.

---

## 7. Decisions-log discipline reminder

Three definite log entries this session: workflow move, pinning correction,
unresolved-declaration sentinel. Each new `D-###` follows the existing
ADR-style template — `Area`, `Supersedes`, `Superseded by` headers, short
body pointing at the spec doc for detail, summary index updated in
lockstep with the full entry, footer changelog updated.

Do not invent decisions, do not collapse two distinct rulings into one
entry. The log's value is grep-ability — one decision, one entry, one
number.

The workflow-move entry (Entry 1) is a _refinement_ of D-302, not a
supersession. Do not mark D-302 as superseded; instead, the new entry's
body should say "refines the production mechanism of D-302 without
changing the deliverable" and cross-reference. This matches the corpus's
existing distinction between supersession (replaces a decision) and
refinement (adds detail to a decision that remains the authority).

The unresolved-declaration sentinel entry (Entry 3) is an _addendum_ to
D-137 in the same sense — D-137 says "non-null after type-check", Entry
3 says "and here is the sentinel for the unresolved path". Same
refinement framing.
