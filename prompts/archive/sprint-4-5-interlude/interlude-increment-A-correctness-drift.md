# Interlude Increment A — Correctness & Drift

> Self-contained increment prompt. Sprint 4 → 5 interlude. Adds no language
> surface. Run against a fresh repo zip in its own session. Deliver full files
> only, in a single zip. Propose before code — plan mode is the approval gate.

-----

## 0. Objective

Bring the design corpus and the implementation to a single reconciled state,
then land a permanent, CI-enforced consistency gate so the corpus and code
cannot silently drift apart again.

Two parts:

- **A1 — Reconciliation.** A one-time pass that finds and closes existing drift
  between the corpus, the code and the wiki. Findings are surfaced with
  evidence, not silently resolved. Forks become a decision, not a workaround.
- **A2 — Drift gate.** A mechanical agreement suite that runs on every commit
  and fails the build on drift. This is the load-bearing deliverable. A1 exists
  to get the corpus green for A2.

The model already exists in the codebase twice. D-308's `ErrorCatalog`
agreement test parses `grob-error-codes.md` and asserts the catalog agrees with
the registry. D-313's `Grob.BenchCheck` reads a frozen baseline and a policy and
gates without ever writing. A2 generalises the first pattern to the rest of the
corpus.

-----

## 1. Why this is worth a gate, not an audit

A one-time audit is correct on the day it runs and stale the day the next
increment lands. The corpus has already drifted in exactly this way:

- The error-code total has read **86**, then **94**, then **98**, then **99** at
  different points. The registry footer itself records a "stale 86 → 94"
  correction. The number is stated in prose in more than one document and
  nothing keeps those statements in agreement.
- ADR cross-references have drifted before — the changelog records bulk
  `ADR-0007 → ADR-0012` and `ADR-0008 → ADR-0013` corrections across multiple
  documents.
- A D-### collision (D-286) is on record.

Each of these is mechanically detectable. A test that fails the build the moment
a count, a reference or a number goes inconsistent is the durable answer. Build
it once; it pays for itself on every subsequent sprint.

-----

## 2. Closed surface — do not touch

This increment is additive. It does **not** modify:

- Anything under `src/` that implements language behaviour — lexer, parser, type
  checker, compiler, VM, stdlib semantics.
- The language content of any spec document (grammar, type rules, error
  semantics, opcode behaviour).
- `ErrorCatalog` descriptor values or the error registry rows. A2 *reads* these;
  it never edits them. If A1 finds a registry error, surface it as a finding —
  do not edit the registry as part of this increment without an explicit
  decision.

It **may** add and edit:

- A new test project `tests/Grob.Consistency.Tests`.
- An optional console wrapper `tooling/Grob.DriftCheck`.
- Non-language corrections to documents flagged in A1 (stale counts, stale
  status rows, broken references) — each tied to a surfaced finding.
- The decisions log, for the D-316 entry and any finding that resolves to a
  decision.

-----

## 3. A1 — Reconciliation pass

Four strands. For each, the discipline is **stop and surface**: report every
finding with file and line or a reproducing grep, and do not silently fix
anything that changes meaning. A correction to a stale count is a fix; a
divergence between code behaviour and a spec rule is a **finding that needs a
decision**.

### 3.1 Decisions-log completeness and integrity

Confirm every implementation decision taken across Sprints 1–4 has a D-###
entry. Walk the Sprint 1–4 increment history and list any decision made in code
that is not recorded. For each present entry, verify the four-location lockstep
holds: summary index row, full entry, status table where applicable, footer
changelog. Report any entry missing a location.

### 3.2 Code ↔ spec reconciliation, front-half surface

The lex → parse → type-check → control-flow surface is frozen for the interlude
(Sprint 5 has not started). Reconcile what the code does against what the spec
says for this surface only. Where the code and a spec rule diverge, that is a
finding. Resolve each as either: a spec correction (cite the authorising D-###),
a code correction (out of this increment's scope — raise it, do not fix language
code here), or a new decision recording the divergence as intended.

### 3.3 Cross-document count and reference drift

Find every place a count or a cross-reference is stated in prose and confirm it
matches reality:

- The error-code total stated anywhere must equal the registered count (live: 99
  across 7 categories).
- Every `ADR-00NN` reference must resolve to a published ADR in `docs/wiki/ADR/`.
- The Project Status milestone table must not contradict the actual sprint
  position — the "Sprint 1" implementation row is stale and is a confirmed
  finding to correct.

### 3.4 ErrorCatalog and gold-master confirmation

D-308's agreement test already guards `ErrorCatalog` against the registry.
Confirm it is green at the current 99 codes. Confirm the gold-master
error-examples fixtures still match current diagnostic output. Neither is edited
here — this strand is a confirmation that the existing mechanical guards pass, so
A2 builds on a green base.

**A1 deliverable:** a findings list — each finding with evidence, a
classification (count fix / reference fix / status fix / spec correction with
D-### / code finding raised-not-fixed / new decision), and the action taken.

-----

## 4. A2 — Drift gate

A new xUnit project `tests/Grob.Consistency.Tests` that runs as part of the
normal `dotnet test` on every commit. Unlike benchmarks, this has no separate
cadence — it is a correctness gate and runs continuously. It is stateless and
reads the canonical documents from `docs/design/` and the wiki from
`docs/wiki/ADR/` at repo-resolved paths.

The checks below are **self-relative** — they compare two live facts against each
other (a stated count against an actual count, a reference against a target).
They need no frozen baseline. The BenchCheck frozen-origin pattern remains
available if a future check needs a point-in-time snapshot, but none of these
do, so do not introduce a `drift.origin.json`.

### 4.1 Required checks

1. **Error-code count agreement.** Parse the summary index in
   `grob-error-codes.md`, count the registered codes, and assert it equals
   `ErrorCatalog.All.Count`. Assert it also equals any "_N codes_" total stated
   in the registry footer. This closes the 86 / 94 / 98 / 99 class permanently.

2. **Decisions-log lockstep.** Parse `grob-decisions-log.md` and assert:
   - No duplicate `D-###` in the summary index.
   - Every index `D-###` has a matching `### D-### —` full entry, and every full
     entry has an index row.
   - Every `Superseded by: D-NNN` and `Supersedes: D-NNN` points to a `D-NNN`
     that exists.

3. **ADR reference integrity.** Collect every `ADR-00NN` mention across
   `docs/design/*.md` and assert each resolves to a file under `docs/wiki/ADR/`.
   This closes the `ADR-0007 → 0012` / `ADR-0008 → 0013` class.

4. **OpCode and TokenKind completeness.** Parse the opcode list the spec declares
   complete from Sprint 2 (`grob-v1-requirements.md` §3.3 and
   `grob-vm-architecture.md`) and assert every named opcode exists in
   `Grob.Core.OpCode`. Do the same for the TokenKind set declared complete from
   Sprint 1 (§3.4). This is the "spec says X exists — does the code have X"
   check.

5. **ErrorCatalog agreement reference.** Do not duplicate D-308's test. Assert it
   is present and discoverable as the catalog↔registry guard, so the consistency
   suite is the single index of every mechanical agreement check in the build.

### 4.2 Parsing discipline

The documents are hand-maintained markdown. Parse defensively: a check that
cannot locate its anchor section must fail loudly with a message naming the
document and the expected anchor, never pass by silently finding nothing. A
green result must mean "checked and agreed", never "found nothing to check".

### 4.3 Optional console wrapper

`tooling/Grob.DriftCheck` — a console entry over the same check library for local
`dotnet run` use, mirroring where `Grob.BenchCheck` lives. The xUnit suite is the
gate; the console is a convenience. If included, it shares the check
implementations with the test project — no second copy of the logic.

-----

## 5. Verification — negative and positive proof per check

TDD-adapted. For each A2 check, prove it fails before it passes:

- **Negative proof.** Plant a violation and confirm the check fails with a clear
  message: a stated count off by one; a duplicate D-###; an `ADR-9999` reference
  with no target; a spec-listed opcode removed from the enum. Confirm the failure
  message names the document and the discrepancy.
- **Positive proof.** With the corpus reconciled by A1, confirm every check
  passes.

Record both for every check. A check with no demonstrated failure mode is not
trusted.

-----

## 6. Decision to log — D-316

Create the authorising entry. Confirm **D-316** is still the first free number
against the live log before writing; the canonical on-disk log outranks this
prompt.

Four-location lockstep: summary index row, full entry, status table if
applicable, footer changelog.

**Entry content:**

- **Title:** Corpus consistency regime — one-time reconciliation plus permanent
  CI-enforced drift gate.
- **Area:** Tooling — quality gate.
- **Body:** A1 reconciles the corpus to a green floor. A2 adds
  `tests/Grob.Consistency.Tests`, a stateless agreement suite running on every
  commit, asserting error-code count agreement, decisions-log lockstep, ADR
  reference integrity, and opcode/TokenKind completeness, with D-308's
  `ErrorCatalog` agreement test referenced as the catalog↔registry guard.
  Generalises the D-308 pattern to the rest of the corpus. Self-relative checks,
  no frozen baseline. Makes the consistency floor enforceable rather than
  aspirational.

Log any A1 finding that resolves to a decision as its own D-### in the same
lockstep, allocated after D-316.

-----

## 7. Deliverable

A single zip containing:

- `tests/Grob.Consistency.Tests/` — full project, all check files, the negative
  and positive proofs.
- `tooling/Grob.DriftCheck/` — if included.
- Full updated copies of every document edited in A1.
- The full updated `grob-decisions-log.md` with D-316 and any finding decisions.
- A1 findings list as `interlude-A-findings.md`.

Full files, never patches. One concern per branch; main is protected.
