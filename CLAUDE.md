# Grob — project memory

Grob is a statically typed scripting language with a stack-based bytecode VM,
written in C# 14 / .NET 10. It targets the gap between Go (too ceremonious),
PowerShell (syntactically hostile) and Python (untyped at scale), for
developers and sysadmins on Windows and Linux and the agents that script on
their behalf (D-441, D-442). It is a serious language, not a
toy — when a choice is between the more instructive path and the better
language, choose the better language.

This file is durable context, always loaded. Per-increment prompts inline the
load-bearing rules specific to their increment; this file holds the rules that
hold across every increment. **The decisions log (`docs/design/grob-decisions-log.md`)
is the authority. Where this file and the log disagree, the log wins — and tell
Chris.**

## How we work

- **Plan before code.** Work in plan mode. Present a numbered implementation
  plan — the type-checker arms, the compiler emission, the VM arms, the tests —
  and wait for Chris's approval before editing anything. The plan is the gate.
- **TDD is non-negotiable.** Tests are written and confirmed _failing_ before
  the implementation that makes them pass. Never skip the red step.
- **One concern per branch.** `main` is protected — never commit to it. Use
  `/start-branch` to propose and create the feature branch, `/commit-message`
  to draft the conventional-commits message (it refuses on `main`). After the
  local commit, push the feature branch — never push to `main` — then open the
  PR, or update its description if one exists, and report the branch, head SHA
  and PR URL. A session never ends with pushed commits and no PR. Merging is
  Chris's action. A command or agent that says to stop before the push still
  stops there. If you find yourself on `main` with edits, stop immediately and
  surface it.
- **PRs are written and sized for review.** The title is in conventional-commits
  form. The description carries a summary, the change groups with their commit
  SHAs, the test and coverage results and anything not run. A PR stays under 100
  changed files, CodeRabbit's limit: if a branch would exceed it, stop and
  propose a split before opening. `/close-session` is the procedure.
- **Scripts go in files.** A multi-line script, fixture or patch is written with
  the Write tool to `.grob-session/`, which is gitignored scratch, and run from
  there — never a heredoc, never inline `sed` with regex escapes. Files are
  edited with the Edit tool. Never run a bare `cat` with no file argument.
- **Navigate by symbol, read with Read.** For a symbol question in C# — a
  definition, its references, implementers, call sites or the impact of a
  rename — use the LSP tool when the session has it. Where it does not (cloud
  and planning sessions), Grep for the declaration and the bare name instead.
  The hits are neither exact nor complete: they include comments, strings and
  unrelated members of the same name, and miss uses that never spell the name,
  such as a `using` alias or a `var` binding. Report a text-based impact search
  as unconfirmed until LSP or, for a rename, a clean build has checked it.
  Otherwise Grep is for text: strings, comments, docs and error-code literals.
  Read a file or a range of lines with the Read tool and its offset and limit,
  never `sed`, `awk`, `head` or `tail` on a file. `sed` and `awk` are denied in
  `.claude/settings.json`; trimming command output with `head` or `tail` is
  still fine.
- **Verify before relying on a citation.** When a prompt cites a `D-###` or a
  `§`, grep the corpus and confirm it says what the prompt assumes. If it has
  been superseded, renumbered or drifted, surface it — do not invent a
  resolution in either direction.
- **Surface, don't silently resolve.** A genuine design fork, a spec/code
  mismatch or a discrepancy between a prompt's framing and the file state is a
  thing to raise, not to quietly paper over.

## Hard rules that gate acceptance

- **`ErrorCatalog` (D-308).** Every diagnostic is raised through a central
  catalog descriptor. The `"Exxxx"` string for any code appears _exactly once_
  in the solution — its descriptor. Never a string literal at a call site.
  Codes are immutable once shipped (ADR-0017), so allocating one is a deliberate
  act, never a casual one. Each increment declares its **error-code budget** — the
  codes it expects to touch or add — and those are pre-authorised. When a
  diagnostic needs a code outside that budget, the judgement call —
  _fold into an existing code, or register a new one_ — is surfaced for a
  decision (the E0012-not-E1002 call in D-330 is the model). Once that call is
  made the mechanics follow `allocating-an-error-code`: the next free number in
  the correct category range taken from the **live** registry, three-location
  lockstep, the count reconciled against the live total rather than a prompt's
  arithmetic, ratified by the D-316 gate on the commit. Never take a number from
  memory or a stale prompt, and never fold into an ill-fitting code to dodge the
  conversation.
- **The §3.1.1 invariant.** Every identifier node carries a non-null
  `ResolvedType` and a non-null `Declaration` after type-check. At error paths
  the sentinels are `GrobType.Error` (type side) and `UnresolvedDecl.Instance`
  (declaration side, D-311) — never literal `null`, never an unset default.
  Assert the sentinel by reference (`Assert.Same`). This is a hard acceptance
  criterion, not a nicety.
- **Closed surfaces, with sanctioned ways to grow.** The `OpCode` enum carries a
  real wire-format stability contract (ADR-0013, the `.grobc` format), so it is
  closed by default and grown only deliberately via `adding-an-opcode`, never
  casually. The parser and AST carry **no** such contract — they are
  compiler-internal and never serialised — but the front end is built
  incrementally, a feature's grammar surface landing with that feature, so the
  parser and AST are likewise treated as closed _within an increment_. Most
  increments are type-checker, compiler-emission, VM-opcode-arm and CLI work over
  already-parsed nodes, and an increment never silently grows the grammar or the
  enum. If a feature genuinely needs a node, follow `extending-the-grammar`; if it
  needs an opcode, follow `adding-an-opcode`. An increment that was not authorised
  to do that work still stops and proposes first — growth is sanctioned and
  logged, never casual and never silent. (`TokenKind` follows the `OpCode`
  rule.)
- **The strict DAG holds.** `Grob.Core` is the only shared ground.
  `Grob.Compiler` references `Grob.Core` and `Grob.Runtime`, never `Grob.Vm`.
  `Grob.Vm` references `Grob.Core` and `Grob.Runtime`, never `Grob.Compiler`.
  `Grob.Cli` is the single composition root — it references every `src/`
  assembly except `Grob.Lsp`, a separate executable, and nothing references it
  back. `src/CLAUDE.md` has the full table.
- **C# 14 / .NET 10 (D-310).** `LangVersion 14` is canonical, from the first
  line.
- **Test routing (§3.5).** Each test goes to the project matching its kind —
  `Grob.Compiler.Tests`, `Grob.Vm.Tests`, `Grob.Integration.Tests` and so on.
- **Coverage.** 90% line and branch coverage is a hard floor, on the whole (D-328)
  and on every PR's new code (Chris's ruling, recorded in D-438), and more is
  always wanted: 90% is the least acceptable
  figure, not a target to stop at. SonarCloud's free-plan PR gate fails only
  below 80% on new code and cannot be raised, so a green Sonar gate does not mean
  the bar is met — new code below 90% is a finding to fix before the PR is
  handed over. `[ExcludeFromCodeCoverage]` requires a substantive `Justification`.

## Quality gates

- **`pre-commit` framework** (`.pre-commit-config.yaml`, not Husky) drives the git
  hooks. The **pre-commit** stage runs TruffleHog secret scanning, file hygiene
  (trailing whitespace, end-of-file newline, YAML/JSON validation, merge-conflict
  markers, large-file block, LF/CRLF line-ending enforcement) and
  `dotnet format --verify-no-changes` scoped to staged `.cs` files, plus
  `tooling/prose-check.ps1`, a warn-only house-style check on staged markdown. The **pre-push**
  stage runs `tooling/coverage-gate.ps1` — `dotnet test` with coverlet's OpenCover
  collector — failing the push if overall line coverage drops below 80% (a coarse
  local proxy for SonarCloud's new-code floor, ratcheting up; distinct from the 90%
  line-coverage bar above). Every clone runs `pre-commit install` once.
- **CodeRabbit** is the in-loop pre-PR reviewer (a deliberate step before the
  PR is opened). The `grob-reviewer` subagent is an optional self-review pass
  before it, and `grob-design-reviewer` audits the corpus; neither replaces
  CodeRabbit.
- **SonarCloud** runs in CI via the `dotnet-sonarscanner` workflow
  (`.github/workflows/sonarcloud.yml`) on push and pull request. A scoped local
  gate runs the same analyzer: `Directory.Build.props` adds `SonarAnalyzer.CSharp`
  to every non-test project and `.editorconfig` opts in a handful of rules
  (`src/CLAUDE.md`, _Static analysis_). `TreatWarningsAsErrors` in
  `Directory.Build.props` enforces the SDK Roslyn analyzers, those rules and the
  `NuGetAudit` advisory gate (D-317).
  **CodeQL** runs in CI via GitHub Advanced Security default setup (too slow for
  per-push local execution); there is no `codeql.yml` and none should be added.
- **External QA cold-read.** At sprint close, the QA brief is the instruction
  file for an in-repo **GPT-5.3 Codex** (Codex CLI) run against the merged
  branch — a different model family, to catch correlated blind spots. This is
  not a Claude task.

## Model policy

Tiers are named without versions: the `sonnet`, `opus`, `fable` and `haiku`
aliases track the current release, so this section does not go stale.

- **Sonnet (high effort)** is the default session workhorse for code generation —
  the settled, rules-inlined transcription work.
- **Opus (high effort)** is for planning, review and judgement — plan mode on a
  design that is not obvious, the reviewer agents, PR-comment triage.
- **Fable (high effort)** is reserved for the named deep structural sub-problem,
  gated behind "only if _this specific thing_ gets fiddly", never "this part is
  hard". It is reached through a subagent run with the `fable` model, invoked
  for the named sub-task only. No agent under `.claude/agents/` pins it.
- **Haiku** for genuinely mechanical, self-contained arms.

**The increment prompt's session table names the model and effort for each
session** (D-431 Decision 5), and its session-start check enforces it. Where the
table and this section differ, the table wins for that increment. `/model-select`
is the procedure for work no prompt covers.

## Output and prose conventions

- Diagnostics and errors to **stderr**; results to **stdout**. Quiet on success,
  clear on failure. **Never emoji** in compiler or CLI output.
- Every file — design document or code — is **edited in place with the Edit
  tool**, in the working tree. Nothing is delivered as a whole replacement file or
  a diff.
- **British English** in documentation; **Oxford comma never**; never the word
  "simply".
- Decisions-log entries follow ADR style (`D-###`, `Area`, `Supersedes`,
  `Superseded by`), updated in **three-location lockstep**: full entry, summary
  index row, footer changelog — the `logging-a-decision` skill is the procedure.

## Session degradation

Watch for these while you work:

- Re-reading a file already read this session.
- Restating a constraint already established.
- Asking a question already answered.
- Drifting outside the increment's stated scope.
- Crossing the plan-mode or read-only gate without an explicit approval turn.

Any one alone is ordinary. Two together mean your grip on your own context has
slipped, and continuing costs more than stopping. Say so plainly and offer
`/handoff`. Do not push on, and do not wait to be asked.
