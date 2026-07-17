# Interlude — Harness and Token-Baseline Audit

**Type:** Interlude (workflow/tooling — no language, compiler, VM or stdlib changes)
**Runs:** Before Sprint 9 increment B
**Branch:** `chore/harness-token-audit` (use `/start-branch`; refuse on `main` as usual)
**Session shape:** Read-only audit → report → explicit approval → apply approved fixes only

---

## Objective

Audit the Claude Code harness for context cost and workflow friction, then apply
approved fixes. The goal is a measurably lower per-turn token baseline and fewer
review-cycle round trips, with zero change to engineering behaviour, quality gates
or the design corpus.

This is the same prompt-tightening instinct applied after Sprint 5, aimed at the
token baseline instead of correctness.

---

## Inputs Chris supplies at session start

Paste the raw output of each into the chat before Phase 1 begins. Do not proceed
without them — the file-level audit must reconcile against what the runtime
actually reports.

1. `/doctor` output (full, including the fix proposals it offers)
2. `/context` output (per-component breakdown: system prompt, CLAUDE.md files,
   MCP servers, skills, subagents)
3. `/usage` output (per-skill / per-subagent / per-MCP attribution, 7-day view)

If any command output conflicts with what the audit finds on disk, surface the
conflict in the report rather than resolving it silently.

---

## Phase 1 — Read-only audit (no edits of any kind)

Investigate each area below. Every finding must cite the file path and, where
relevant, an estimated token weight (chars ÷ 4 is an acceptable estimate — state
the method used). Findings without evidence are not findings.

### 1.1 CLAUDE.md inventory and placement

- Locate every CLAUDE.md in the repository. Known state: files exist at `src` and
  `test` level but not at individual project level. Confirm the actual set.
- For the root file and each directory-level file, classify every section as one of:
  - **(a) Derivable** — Claude could infer it from the code or solution structure.
    Candidate for deletion.
  - **(b) Folder-specific** — only relevant when working inside one project.
    Candidate for relocation to a nested per-project CLAUDE.md
    (e.g. `src/Grob.Compiler/CLAUDE.md` for error-recovery invariants,
    `bench/CLAUDE.md` for the D-313 ratchet rule and `policy.json` semantics,
    `test/CLAUDE.md` for the quarantine-over-weakening rule and framework
    constraints).
  - **(c) Genuinely global** — must load every turn (branch discipline, British
    English / no Oxford comma, full-files-never-patches, plan-mode gate).
  - **(d) Duplicated or stale** — repeats a skill, a command, another CLAUDE.md
    or a superseded convention.
- Produce a target layout: which files exist after the change, what each contains,
  and the estimated per-turn saving for the most common working directories.
- Rule: content is moved or trimmed, never lost. Anything deleted as (a) or (d)
  must be quoted in the report with the reason.

### 1.2 Skills, commands and agent configurations

- Inventory everything under `.claude/skills/`, `.claude/commands/` and any agent
  configuration files. For each item record: size, purpose, evidence of use
  (cross-reference `prompts/archive/sprint-N/` and recent increment prompts) and
  overlap with CLAUDE.md content or other skills.
- Flag as candidates: unused since Sprint 6 or earlier, near-duplicates,
  skills whose body could shrink by pointing at a repo file instead of inlining it.
- For skills that should never fire autonomously, note whether
  `disable-model-invocation` is set.
- Recommendation only in Phase 1 — nothing is removed without the archive evidence
  attached.

### 1.3 Tool-output volume

- Identify the noisy commands in the standard increment loop: `dotnet build`,
  `dotnet test`, BenchmarkDotNet runs, `Grob.BenchCheck`, `git diff` on large
  changes.
- For each, propose a filter wrapper under `tooling/` (PowerShell, Windows-native)
  that passes through failures and summaries only. Reference targets:
  - `dotnet test`: on green, one summary line (counts + duration); on red, failing
    test names plus the assertion/exception block only.
  - BenchCheck: pass/fail verdict plus breaching benchmarks with deltas only.
  - Build: errors and warnings only, no restore/progress noise.
- Check `settings.json` for output caps (`BASH_MAX_OUTPUT_LENGTH` or current
  equivalent — verify the current key name against the live Claude Code docs
  rather than assuming). Propose values.

### 1.4 Hooks — moving review-cycle killers in-session

- Current state: Husky.NET pre-push gate exists. The gap is the class of
  mechanical failure that today costs a CodeRabbit round trip.
- Evaluate and propose (do not yet implement):
  - **PostToolUse** on markdown edits: Oxford-comma pattern check
    (`, and ` / `, or ` in list contexts — design the pattern to avoid false
    positives on legitimate clause boundaries; if a reliable pattern is not
    achievable, say so and propose a warn-only variant).
  - **PostToolUse** on C# edits: `dotnet format --verify-no-changes` scoped to
    touched files.
  - **PreToolUse** guard: refuse file edits while on `main` (defence in depth
    alongside the existing command-level refusals).
- For each proposed hook: trigger, script location, failure behaviour
  (block vs warn) and estimated cost per invocation. Slow hooks are a finding,
  not a feature — anything `/doctor` flagged as slow gets addressed here.

### 1.5 Increment-prompt boilerplate

- Sample the three most recent increment prompts from `prompts/archive/`.
  Identify repeated boilerplate that is also present in CLAUDE.md or a skill —
  double-loading is pure waste.
- Propose the minimal canonical split: what lives in the prompt (increment-specific
  scope, acceptance criteria), what lives in CLAUDE.md (global rules), what lives
  in a skill (procedures invoked on demand). One home per rule.

### 1.6 Settings and session ergonomics

- Review `settings.json` (all scopes present in the repo) for: status line with
  live context percentage, model defaults per subagent, output caps, anything
  `/doctor` flagged.
- Note prompt-cache behaviour as guidance only (no config change): the cache is
  warm for roughly five minutes; long idle gaps mid-increment forfeit up to 90%
  cheaper cached reads. Add one line to the report on whether the current session
  pattern respects this.

---

## Phase 1 deliverable — the report

Produce `docs/design/harness-audit-report.md` containing:

1. **Baseline** — current estimated fixed per-turn cost (CLAUDE.md total + skills
   loaded at start + MCP definitions), reconciled against the `/context` paste.
2. **Findings** — ranked by estimated saving, each with file path, evidence and
   the proposed fix. Separate per-turn savings (CLAUDE.md, skills) from
   per-incident savings (output filters, hooks) — they compound differently.
3. **Projected baseline** — the same figure after all proposed fixes, stated as
   a range, not a promise.
4. **Change list** — numbered, each item independently approvable. Chris approves
   by item number ("apply 1, 3–7"). No bundling.
5. **Out-of-session items** — anything requiring a planning-session decision
   (e.g. if nested CLAUDE.md placement or filter wrappers become standing policy
   worth a D-### entry, flag it — do not allocate a number; D-### numbers are
   authored in planning sessions against the live log tail).

**STOP after the report. Do not edit anything until Chris approves specific items.**

---

## Phase 2 — Apply approved fixes only

- Implement exactly the approved item numbers, nothing else discovered en route.
  New findings mid-implementation go in a follow-up list, not the working set.
- Full files, never patches. Nested CLAUDE.md files are complete documents.
  Wrapper scripts land under `tooling/` with a one-line usage comment.
- Every deletion from a CLAUDE.md or skill appears in the commit message body
  with its classification ((a) derivable or (d) duplicate) — the commit is the
  audit trail.
- Re-estimate the baseline after changes and append an **Outcome** section to the
  report with before/after figures.
- `/commit-message` as usual. One commit per logical group is acceptable; one
  giant commit is not.

---

## Guardrails

- **No design-corpus edits.** `docs/design/*.md` other than the new report,
  the decisions log, error registry and spec docs are all out of scope. If a
  corpus inconsistency is noticed, record it in the report and move on.
- **No quality-gate weakening.** Benchmark baselines, `policy.json`, Husky
  pre-push, SonarCloud config and test projects are untouchable in this session.
- **No behaviour changes.** Nothing in this session may alter what the compiler,
  VM or tests do. If a proposed fix could, it is out of scope by definition.
- **Nothing is deleted without evidence.** Move or trim with the content quoted
  in the report; unused skills are archived to `prompts/archive/harness-audit/`,
  not deleted.
- British English throughout. No Oxford commas. Never "simply".

---

## Acceptance criteria

1. Report exists with baseline, ranked findings, projected baseline and a
   numbered change list — every finding evidenced with a path and an estimate.
2. All approved items applied; no unapproved edits anywhere in the diff.
3. Fixed per-turn baseline measurably reduced (verify with `/context` in a fresh
   session after merge; record the figure in the Outcome section).
4. Each rule has exactly one home across CLAUDE.md files, skills and increment
   boilerplate — no double-loading remains for the audited set.
5. Output filter wrappers demonstrably work: one green `dotnet test` run and one
   deliberately red run shown in filtered form in the session.
6. Solution builds, full test suite green, pre-push gate passes — proving the
   session changed cost, not behaviour.
