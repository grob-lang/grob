# Harness and Token-Baseline Audit — Phase 1 Report

**Session:** Interlude, before Sprint 9 Increment B. Branch: `chore/token-optimisation-audit`.
**Status:** Read-only findings. Nothing has been edited. Awaiting approval by item number.

Method: token estimates use chars ÷ 4 unless a figure is quoted directly from the
supplied `/context` paste. File sizes are `wc -c` on the working tree at HEAD of this
branch (no diff from `main` at time of writing).

---

## 1. Baseline

From the supplied `/context` paste (`claude-sonnet-5`, session total 164.6k/967.0k, 17%):

| Component | Tokens | Share |
|---|---|---|
| System prompt | 9.5k | 1.0% |
| System tools | 18.1k | 1.9% |
| Custom agents | 1.2k | 0.1% |
| Memory files | 2.6k | 0.3% |
| Skills (listing) | 7.9k | 0.8% |
| **Fixed baseline (sum of the above)** | **~39.3k** | **~4.1%** |
| Messages (this session) | 128.3k | 13.3% |
| Autocompact buffer | 33.0k | 3.4% |

The fixed, every-turn cost — the thing this audit can actually move — is **~39.3k
tokens**, not the 164.6k session total (most of that is this session's own message
history, which is conversation-specific, not harness cost).

Reconciliation against disk:

- **Memory files: 2.6k** matches `MEMORY.md` exactly — no drift.
- **Skills: 7.9k** is the always-resident *description* line for each of the 14
  project skills (bodies load only on invocation) — consistent with `/doctor`'s
  per-skill estimates (`doctor-audit.md` lines 6–20), which sum to ~1.19k using its
  own narrower method; the `/context` figure of 7.9k is the authoritative one since
  it is a live measurement, not a chars÷4 estimate, and it likely also carries the
  skill-listing wrapper overhead `/doctor`'s per-skill line-items don't itemise. I
  cannot fully reconcile the two to the token — flagging the discrepancy rather than
  picking one.
- **System prompt: 9.5k** is not broken out by `/context` into "base system prompt"
  vs "root `CLAUDE.md`". Root `CLAUDE.md` alone is 7,879 chars ≈ 1,970 tokens
  (`wc -c CLAUDE.md`), consistent with `/doctor`'s own 1,970-token estimate
  (`doctor-audit.md` line 29). If root `CLAUDE.md` is folded into "System prompt",
  that leaves roughly 7.5k tokens for the Claude Code base system prompt itself,
  which is outside this audit's control.
- **Custom agents: 1.2k** matches `/doctor`'s per-agent estimates
  (300+243+200+158+121 = 1,022, same order of magnitude; the `/context` figure is
  the one to trust).

`src/CLAUDE.md` (23,901 chars ≈ 5,975 tokens), `tests/CLAUDE.md` (5,112 chars ≈
1,278 tokens) and `plugins/CLAUDE.md` (3,286 chars ≈ 822 tokens) are **not** part of
the fixed baseline above — they load only when the working directory is under
`src/`, `tests/` or `plugins/` respectively. The `/context` paste was presumably
captured at the repo root, so this is consistent.

---

## 2. Findings

Ranked by estimated saving. Per-turn findings (load every session) are separated
from per-incident findings (fire on a specific action).

### 2.1 Per-turn

**F1 — `CLAUDE.md` "Repo harness map (D-314)" section is derivable — (a).**
`CLAUDE.md` lines naming this section list `CLAUDE.md`, `.claude/commands/`,
`.claude/agents/`, `prompts/<sprint>/` and `docs/design/` with one-line
descriptions — a repo tour reconstructable by `ls` and reading two files. It costs
~280 tokens every turn (9 lines of the always-loaded root file). Quoted for the
record before deletion:

> ## Repo harness map (D-314)
> - `CLAUDE.md` — this file, durable project memory.
> - `.claude/commands/sprint-N-*.md` — invokable increment prompts (`/sprint-4-a`…).
> - `.claude/agents/*.md` — subagents (e.g. the Opus lowering specialist).
> - `prompts/<sprint>/` — kickoff records, QA briefs and archive copies of the
>   increment commands. The retired `.github/` Copilot harness lives here in
>   history for the sprints it drove.
> - `docs/design/` — the working design corpus; the decisions log is the
>   authority. `docs/wiki/` is a published reference rendering.

This agrees with `/doctor`'s independent finding (`doctor-audit.md` line 38).

**F2 — `src/CLAUDE.md` carries the assembly/project mapping twice — (d).**
"Where code belongs (the DAG)" (lines 20–33) and "What goes where" (lines 105–117)
both map concern → project, with overlapping rows. The DAG table additionally
carries a "May reference" column the second table lacks, and is the one referenced
elsewhere in the file ("If a change requires a new project reference..."). Consolidate
into the DAG table; drop "What goes where" entirely. Saves ~150 tokens whenever
working under `src/`. Agrees with `/doctor` (`doctor-audit.md` line 39).

**F3 — the "four-location lockstep" decisions-log claim disagrees with the
`logging-a-decision` skill, and the fourth location looks stale — (d), needs a
judgement call.**
Root `CLAUDE.md` states: "Decisions-log entries follow ADR style..., updated in
**four-location lockstep**... summary index row, full entry, status table, footer
changelog." The `logging-a-decision` skill's own checklist ("Keep three things in
lockstep", `SKILL.md` lines 52–59) lists only **three**: full entry, summary index,
footer changelog — no status table. Searching `grob-decisions-log.md` for "status
table" finds it referenced only around two early entries (lines 2872–2880, 5987–5990)
describing a one-off "project-status table" from Sprint 1's clox-preparation gate,
not a recurring per-entry artefact. This looks like root `CLAUDE.md` describing a
Sprint-1-era practice that never became a standing requirement, while the skill
(added later, presumably against real usage) correctly has three. **This is Chris's
call, not mine to resolve silently**: either (a) the "status table" line is stale and
should be dropped from `CLAUDE.md` entirely, matching the skill's three, or (b) a
per-decision project-status table is still intended and the skill's checklist is
missing a step. Recorded here rather than guessed either way. If (a), trim
`CLAUDE.md`'s paragraph to a one-line pointer at the skill (~40 tokens saved/turn,
plus the double-loading — the skill also loads its full body on the 14 times it was
invoked per `/doctor` — is resolved).

**F4 — Quality-gates section names the wrong tool: "Husky.NET" doesn't exist in
this repo; the actual gate is the Python `pre-commit` framework — factual drift,
not (a)/(b)/(c)/(d), but the kind of on-disk-vs-doc conflict Phase 1 is asked to
surface.**
Root `CLAUDE.md`'s "Quality gates" section states: "**Husky.NET pre-push** runs
build, test and format verification before every push." On disk:
- No `.husky/` directory, no `dotnet-tools.json` entry for Husky
  (`.config/dotnet-tools.json` lists only `dotnet-sonarscanner` and `cyclonedx`), no
  `package.json`.
- `.git/hooks/pre-commit` and `.git/hooks/pre-push` are both generated by
  **pre-commit** (https://pre-commit.com), driven by `.pre-commit-config.yaml`.
- The actual gate: **pre-commit stage** runs TruffleHog secret scanning, file
  hygiene (trailing whitespace, EOF newline, YAML/JSON validation, merge-conflict
  markers, large-file block, LF/CRLF line-ending enforcement) and
  `dotnet format Grob.slnx --verify-no-changes` scoped to staged `.cs` files.
  **pre-push stage** runs `tooling/coverage-gate.ps1` — `dotnet test` with
  coverlet's OpenCover collector, failing if overall line coverage drops below
  **80%** (the SonarCloud new-code floor used as a coarse local proxy, explicitly
  documented in the config's own comments as "ratchet up", not the project's 90%
  line-coverage bar cited elsewhere in `CLAUDE.md`).
- There is no `dotnet build` step and no full `dotnet test` at the pre-commit stage
  — only at pre-push, and only via the coverage-gate script, not as a bare build
  check.

This is a correctness fix, not a token one — restating this accurately is a handful
of lines either way — but it is exactly the class of drift Phase 1 exists to catch,
and it sits in the always-loaded file every session reads as ground truth for what
gates a push. **I am not proposing to change the gate itself** (guardrail: quality
gates are untouchable this session) — only to correct `CLAUDE.md`'s description of
the gate that already exists. Note for Chris: my own persistent memory (outside this
repo) also carries the "Husky.NET" claim — I'll correct that once this is resolved,
independent of anything approved here.

**F5 — no per-turn action on the DAG-summary / C#-version duplication between root
and `src/CLAUDE.md` — reviewed, not a finding.**
Root `CLAUDE.md`'s condensed "The strict DAG holds" paragraph and its "C# 14 / .NET
10 (D-310)" line both restate content also present in full in `src/CLAUDE.md`. I
considered flagging these as (d), but root's compact form is the only copy visible
from `tests/`, `plugins/`, `docs/` or the repo root — `plugins/CLAUDE.md` in
particular relies on the DAG rule without restating it, and `tests/CLAUDE.md`
explicitly says its rules "sit on top of the C# host-code rules in `src/CLAUDE.md`"
(line 5) — a rule that will **not** actually load when working under `tests/`, since
nested `CLAUDE.md` loading walks ancestors of the working directory, not siblings.
Root's short forms are the thing making that cross-reference true in practice. This
is intentional layering (compact global reminder + detailed local rule), and the
duplicated cost when working inside `src/` is small (root's versions are one line
and one short paragraph). Recommend leaving as-is. Flagging the `tests/CLAUDE.md`
cross-reference as worth a one-line footnote in Phase 2 if F3/F4 are being touched
anyway, since it's adjacent — not a separate numbered item on its own.

**F6 — `grob/.claude/settings.local.json` has a dangling MCP server entry — (d).**
`enabledMcpjsonServers: ["microsoft-docs", "github"]` — no `github` server is
defined in `.mcp.json` (which only defines `microsoft-docs`). Harmless (nothing to
enable), but dead configuration. Agrees with `/doctor` (`doctor-audit.md` line 44).

**F7 — outer `d:/Code/grob-lang/.claude/settings.local.json` duplicates a rule
already in the checked-in outer `settings.json` — (d).**
Both files grant `Bash(python -c ' *)` at the same scope. Agrees with `/doctor`
(`doctor-audit.md` line 44).

### 2.2 Per-incident

**F8 — `BASH_MAX_OUTPUT_LENGTH` is unset everywhere.**
Verified against the live Claude Code docs (`code.claude.com/docs/en/env-vars`,
fetched this session — not assumed from training data): the current key is
`BASH_MAX_OUTPUT_LENGTH` (characters, not tokens), set via the `env` block in
`settings.json`. It is not present in `grob/.claude/settings.local.json`, the outer
`.claude/settings.json`/`settings.local.json`, or the global
`~/.claude/settings.json` (which carries only `{"effortLevel": "high"}`). Unset means
the built-in default applies, which is generous enough that a verbose `dotnet build`
or `dotnet test` run on this solution (7 production + 7 test projects) can land
several thousand tokens of restore/progress noise in a single tool result. Proposing
a value in the change list.

**F9 — no output-filter wrappers exist for the noisy commands in the standard
increment loop.**
`tooling/` currently holds `coverage-gate.ps1`, `sonar-local.ps1`, and the
`Grob.BenchCheck` / `Grob.DriftCheck` .NET tools — no wrapper filters stdout for
interactive use. Proposed (design only, per Phase 1 scope — not implemented here):

| Command | Wrapper | Green behaviour | Red behaviour |
|---|---|---|---|
| `dotnet test` | `tooling/test-summary.ps1` | one line: pass/fail counts + duration | failing test names + assertion/exception block only, no passing-test noise |
| `dotnet build` | `tooling/build-summary.ps1` | one line: 0 errors/0 warnings | errors and warnings only, no restore/`Determining projects to restore...` noise |
| `Grob.BenchCheck` | already emits a verdict; confirm it is passed `--quiet`/summary flags in the standard invocation rather than wrapped again | pass/fail + breaching benchmarks with deltas | same |
| `git diff` on large changes | none proposed — `git diff --stat` first, then targeted `git diff -- <path>` is a workflow habit, not a wrapper problem | — | — |

Each wrapper is a thin PowerShell pass-through (`Invoke-Expression` the real command
capturing output, pattern-match summary/failure lines, re-emit, propagate the real
exit code) — Windows-native per the guardrail, no new dependency.

**F10 — proposed Claude Code hooks (design only, not implemented this session,
per the interlude's own Phase 1/Phase 2 split).**

No hooks are configured in any settings scope today (`/doctor` confirms this
independently — "no Claude Code hooks configured anywhere" — and I found none in
`grob/.claude/settings.local.json`, the outer `.claude/settings.json`, or the global
`~/.claude/settings.json`). Note this is a different layer from F4's `pre-commit`
git hooks — these would be in-session, model-facing hooks that catch a mechanical
error before the human even sees a diff, which `pre-commit` (running at commit/push
time) cannot do.

| Hook | Trigger | Script | Failure behaviour | Est. cost/invocation |
|---|---|---|---|---|
| Oxford-comma check | `PostToolUse` on `.md` edits | pattern check for `, and `/`, or ` in list contexts | **Warn-only, not block.** I could not design a pattern free of false positives on legitimate clause boundaries ("the log is a record of decisions, and the format matters" is a valid two-clause sentence, not an Oxford-comma violation) without a real parse of list structure, which a regex-based hook cannot do reliably. Per the interlude's own instruction, saying so rather than shipping an over-eager blocker. | low — single regex pass per edited file |
| `dotnet format --verify-no-changes` | `PostToolUse` on `.cs` edits | scoped to the touched file(s) | **Warn** — this duplicates `pre-commit`'s own `dotnet-format` hook (F4), which already blocks at commit time; an in-session copy only moves the same check earlier, so a warn (not a second hard block) avoids two gates disagreeing on the same file mid-edit | moderate — `dotnet format` invocation has real startup cost per call; batching per-tool-call rather than per-file recommended if adopted |
| Refuse edits on `main` | `PreToolUse` on `Edit`/`Write` | check `git branch --show-current` | **Block.** Defence in depth alongside the existing command-level refusals (`/start-branch` and `/commit-message` already refuse on `main`) | low — one `git` call per edit |

None of these are implemented in this session. `/doctor`'s slow-hook check (Check 5)
found nothing to warn about since none exist yet — if any of these are approved for
Phase 2, cost should be re-measured against that same check afterwards.

**F11 — increment-prompt acceptance-criteria boilerplate duplicates always-loaded
`CLAUDE.md` rules.**
Sampled the three most recently touched prompts in `prompts/archive/sprint-9/`
(`sprint-9-a3.md`, `sprint-9-d.md`, both last touched together with `sprint-9-a2.md`)
against the full set. The phrases `§3.1.1 holds`, `D-316 green`, `coverage at or
above 90%` and `the DAG holds` — each already a "hard rule that gates acceptance" in
the always-loaded root `CLAUDE.md` — appear **28 times across the 10 Sprint 9
prompts** (`grep -c` over `prompts/archive/sprint-9/sprint-9-{a,a2,a3,b,c,d,e,f,g,h}.md`).
Each `.claude/commands/sprint-9-*.md` is a byte-identical mirror of its
`prompts/archive/sprint-9/` counterpart (verified by `diff` on two samples), so the
boilerplate is paid twice on top of that — once in the invokable command, once in the
archive copy — though the archive copy is presumably not itself read during a normal
session (only the command form is invoked).

This is a per-incident cost (paid once when a prompt is read to start an increment,
not every turn), and modest per prompt (roughly 40–60 tokens of restatement per
prompt), but it compounds across a ten-increment sprint and is precisely the "repeated
boilerplate also present in CLAUDE.md" 1.5 asks about. Proposed canonical split:
increment prompts keep increment-specific acceptance criteria (what this increment
must do) and drop the standing, always-true bar (§3.1.1, D-316, coverage, DAG) —
those are guaranteed by `CLAUDE.md` being loaded every session regardless of what the
prompt says, restating them adds words without adding a check. **Scoped
recommendation:** apply only to the Sprint 9 increments that have **not yet run**
(`sprint-9-{b,c,d,e,f,g,h}`, in both `.claude/commands/` and its
`prompts/archive/sprint-9/` mirror) — `sprint-9-{a,a2,a3}` have already executed and
editing their archived form would be rewriting a historical record of what was
actually run, which I'd rather flag than do silently. This is a larger, judgement-
heavy edit (14 files) — happy to hold it for its own approval slot separate from the
mechanical fixes above.

**F12 — `/doctor`'s remaining findings, independently spot-checked.**
- Root `CLAUDE.md` (~1,970 tok), `src/CLAUDE.md` (~5,975 tok), `tests/CLAUDE.md`
  (~1,278 tok), `plugins/CLAUDE.md` (~822 tok) — all four confirmed by `wc -c` above,
  no drift from `/doctor`'s figures.
  Installed Claude Code 2.1.181 vs latest 2.1.211 — not independently re-verified
  (no network check for a version number beyond trusting `/doctor`'s own report),
  low-risk to accept as-is.
- The flagged `SONAR_TOKEN` plaintext exposure (`doctor-audit.md` line 1) is outside
  this audit's scope (a credential-hygiene issue, not a harness-cost or workflow one)
  — repeating `/doctor`'s recommendation to rotate it on SonarCloud if still valid,
  and not investigating further here.
- The 6 "keep despite zero recent use" skills
  (`adding-a-stdlib-function`, `adding-an-opcode`, `authoring-a-plugin`,
  `defining-a-type`, `emitting-closures-and-upvalues`, `extending-the-grammar`) —
  agreed. Sprint 9 B onward is exactly the stdlib/type-carrying work these exist
  for, and Sprint 8/9's `math` namespace and array-element-type work already used
  `extending-the-grammar`/`grob-spec-lookup` once each. No archival proposed.

---

## 3. Projected baseline

Applying F1–F3 (root `CLAUDE.md` trims, assuming F3 resolves toward the shorter
form) and F2 (src consolidation) against the reconciled figures in §1:

- Root `CLAUDE.md`: 7,879 chars → roughly 7,879 − (280+40)×4 ≈ 6,600 chars
  (~1,650 tokens), a saving of ~320 tokens **every turn**.
- `src/CLAUDE.md`: saving ~150 tokens whenever working under `src/` (not every
  turn from repo root).

This is a **stated range, not a promise**: fixed baseline ~39.3k tokens today →
roughly **39.0k tokens** at repo root after F1+F3, and roughly **38.9k tokens**
when working inside `src/` after F1–F3. This is a ~1% reduction in the fixed
baseline. F4 (Husky correction) and F6/F7 (settings dedup) are correctness/hygiene
fixes with negligible token effect. F8–F10 (output caps, wrapper scripts, hooks)
are per-incident, not per-turn — their value shows up as fewer tokens per noisy
tool call and fewer CodeRabbit round trips, not in this baseline figure; I'd
expect these to matter more in practice than the ~1% per-turn number, but I have
no clean way to quantify "round trips avoided" without a longer observation
window.

---

## 4. Change list

Numbered, independently approvable. Approve by number ("apply 1, 3–7").

1. Delete "Repo harness map (D-314)" section from `CLAUDE.md` (F1).
2. Consolidate `src/CLAUDE.md`'s duplicate project-mapping table into the DAG table,
   drop "What goes where" (F2).
3. Resolve the four-vs-three-location decisions-log lockstep drift — **needs Chris's
   call on whether "status table" is still real** (F3). Default proposal if no
   objection: drop it from `CLAUDE.md`, matching the skill's three.
4. Correct `CLAUDE.md`'s "Quality gates" section to describe the actual `pre-commit`
   framework gate instead of "Husky.NET" (F4).
5. Set `BASH_MAX_OUTPUT_LENGTH` in `grob/.claude/settings.local.json` (proposed:
   40000 characters — generous enough for a full `dotnet test` failure block, tight
   enough to cap a clean `dotnet build`/`restore` wall of text) (F8).
6. Remove the dangling `"github"` entry from
   `grob/.claude/settings.local.json`'s `enabledMcpjsonServers` (F6).
7. Remove the duplicate `Bash(python -c ' *)` allow rule from the outer
   `d:/Code/grob-lang/.claude/settings.local.json` (F7).
8. Add `tooling/test-summary.ps1` and `tooling/build-summary.ps1` output-filter
   wrappers (F9).
9. Add the `PreToolUse` main-branch edit guard hook only (block). Hold the
   Oxford-comma hook (warn-only, if approved at all) and the `dotnet format`
   duplicate-check hook (warn-only) as separate sub-items since both are weaker
   than F4's existing `pre-commit` gate and I'd rather Chris explicitly ask for
   warn-only noise than default to adding it (F10).
9a. (sub-item) Oxford-comma `PostToolUse` warn-only hook.
9b. (sub-item) `dotnet format --verify-no-changes` `PostToolUse` warn-only hook.
10. Run `claude update` (2.1.181 → 2.1.211).
11. Trim the restated §3.1.1/D-316/coverage/DAG boilerplate from the
    not-yet-run Sprint 9 increment prompts (`sprint-9-{b,c,d,e,f,g,h}`, both
    `.claude/commands/` and `prompts/archive/sprint-9/` copies) to a single pointer
    line (F11). Larger surface (14 files) — fine to hold for its own slot.

---

## 5. Out-of-session items

- **Nested `CLAUDE.md` placement below `src/`.** The interlude's own framing floats
  `src/Grob.Compiler/CLAUDE.md` for error-recovery invariants as an example. I
  looked for content in `src/CLAUDE.md` narrow enough to justify a fifth file and
  didn't find a strong candidate — the closest (§3.1.1, the error-recovering parser
  rules) is cited constantly by increment prompts touching *other* projects too
  (stdlib registrations, VM tests), so keeping it at the `src/` level looked more
  correct than moving it deeper. Flagging that I made this call without a D-###
  behind it — if nested-`CLAUDE.md`-below-`src/` becomes a standing policy question,
  it belongs in a planning session, not decided here by omission.
- **F4's correction ripples into my own cross-session memory** (a "Husky.NET
  pre-push gate exists" entry) — I'll fix that independently of what's approved
  here; noting it so it isn't mistaken for something this session changed in the
  repo.
- **Whether `pre-commit`'s 80% coverage-gate threshold and `CLAUDE.md`'s 90%
  line-coverage bar should be reconciled or are deliberately two different things**
  (one a local pre-push proxy for SonarCloud's new-code floor, one the project's
  overall bar) surfaced as a byproduct of F4 but is a quality-gate design question,
  not a harness one — out of scope for this session's guardrails, flagging for a
  planning session if the distinction isn't already intentional and documented
  somewhere I didn't find.

---

**STOP — awaiting approval by item number before any edit.**

---

## 6. Outcome (Phase 2 — applied)

Chris approved 1, 2, 4, 6, 7 and 10, with 3 folded into 4, after a hyper-critical
pass on whether the ~1% saving justified the work. The conclusion — recorded here
because it is the honest result of the audit — was that the token angle was never the
real value; correctness was. What was applied:

- **1 — done.** "Repo harness map (D-314)" section deleted from `CLAUDE.md`
  (derivable, ~500 chars).
- **2 — done.** `src/CLAUDE.md`'s "What goes where" table dropped; its one unique row
  (ambient abstractions) was already covered by the DAG table's `Grob.Core`
  ("ambient-dependency interfaces") and `Grob.Cli` ("concrete ambient
  implementations") rows, so nothing was lost. Saved 752 chars (~188 tokens) when
  working under `src/`.
- **3 (folded into 4) — done.** `CLAUDE.md`'s "four-location lockstep" corrected to
  "three-location lockstep (full entry, summary index row, footer changelog)" with a
  pointer at the `logging-a-decision` skill. The "status table" was a one-off
  Sprint-1 clox-gate artefact, not a recurring per-decision step; dropped from
  `CLAUDE.md` to match the skill rather than added to the skill (which would have
  perpetuated a stale requirement).
- **4 — done.** "Husky.NET pre-push" replaced with an accurate description of the
  actual `pre-commit`-framework gate (TruffleHog + file hygiene + scoped
  `dotnet format` at pre-commit; `tooling/coverage-gate.ps1` at 80% at pre-push).
  The gate itself was not touched — only the wrong description of it.
- **6 — done.** Dangling `"github"` removed from
  `grob/.claude/settings.local.json`'s `enabledMcpjsonServers`.
- **7 — done.** Duplicate `Bash(python -c ' *)` allow rule emptied from the outer
  `d:/Code/grob-lang/.claude/settings.local.json` (already granted by the sibling
  `settings.json`). This file is outside the `grob/` git repo, so it is not part of
  any commit — a local-hygiene fix only.
- **10 — Chris's action.** `claude update` (2.1.181 → 2.1.211) runs in an interactive
  session; cannot be done from within a non-interactive run.

Deferred, with reasons, after the critical pass:

- **5 (BASH_MAX_OUTPUT_LENGTH) — dropped, evidence-based.** Measured on this repo: a
  green `dotnet build` is **2,282 chars** and a green full `dotnet test` is **4,252
  chars** (~1,060 tokens). Both are an order of magnitude under the proposed 40000
  cap; a cap that low would only ever fire on a catastrophic failure with hundreds of
  errors — precisely the output you do not want truncated. The noise this item
  assumed does not exist here. Not set.
- **8, 9, 9a, 9b (wrapper scripts, hooks) — shelved.** No observed pain: `/doctor`
  found zero configured hooks and zero slow ones, and the measured build/test output
  above is already modest. Building two PowerShell wrappers and three hooks to solve
  an unobserved problem is effort chasing a hypothesis. Revisit only if a real
  session shows the need.
- **11 (increment-prompt boilerplate trim) — shelved.** ~500 tokens once per sprint
  against a 14-file edit with real risk of silently dropping an acceptance check from
  a not-yet-run increment. Bad trade.

### Before/after

| Metric | Before | After | Delta |
|---|---|---|---|
| Root `CLAUDE.md` | 7,879 chars | 7,965 chars | **+86 chars** |
| `src/CLAUDE.md` | 23,901 chars | 23,149 chars | −752 chars (~188 tok under `src/`) |

Root `CLAUDE.md` grew slightly: item 1's deletion (~500 chars) was outweighed by
item 4's accurate `pre-commit` description costing ~600 chars more than the single
wrong "Husky.NET" line it replaced. **The per-turn token baseline was not measurably
reduced at the root** — the honest outcome. The value delivered by this session is
correctness (the gate description was wrong in the always-loaded file), a real drift
fix (the lockstep count), two small dedups and modest savings when working under
`src/` — not a token reduction. Acceptance criterion 3 ("fixed per-turn baseline
measurably reduced") is **not met at the root and is not worth forcing**; recording
that plainly rather than massaging the figure.

### Behaviour unchanged (acceptance criterion 6)

Only markdown and JSON settings files were edited — no code. Confirmed green after
the changes: `dotnet build Grob.slnx` exit 0; `dotnet test Grob.slnx` exit 0. The
session changed documentation accuracy and configuration hygiene, not behaviour.
