# Grob — project memory

Grob is a statically typed scripting language with a stack-based bytecode VM,
written in C# 14 / .NET 10. It targets the gap between Go (too ceremonious),
PowerShell (syntactically hostile) and Python (untyped at scale), for an
audience of Windows developers and sysadmins. It is a serious language, not a
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
- **TDD is non-negotiable.** Tests are written and confirmed *failing* before
  the implementation that makes them pass. Never skip the red step.
- **One concern per branch.** `main` is protected — never commit to it. Use
  `/start-branch` to propose and create the feature branch, `/commit-message`
  to draft the conventional-commits message (it refuses on `main`). Stop after
  the local commit; pushing and opening the PR are Chris's actions. If you find
  yourself on `main` with edits, stop immediately and surface it.
- **Verify before relying on a citation.** When a prompt cites a `D-###` or a
  `§`, grep the corpus and confirm it says what the prompt assumes. If it has
  been superseded, renumbered or drifted, surface it — do not invent a
  resolution in either direction.
- **Surface, don't silently resolve.** A genuine design fork, a spec/code
  mismatch or a discrepancy between a prompt's framing and the file state is a
  thing to raise, not to quietly paper over.

## Hard rules that gate acceptance

- **`ErrorCatalog` (D-308).** Every diagnostic is raised through a central
  catalog descriptor. The `"Exxxx"` string for any code appears *exactly once*
  in the solution — its descriptor. Never a string literal at a call site.
  Codes are immutable once shipped (ADR-0017), so allocating one is a deliberate
  act, never a casual one. Each increment declares its **error-code budget** — the
  codes it expects to touch or add — and those are pre-authorised. When a
  diagnostic needs a code outside that budget, the judgement call —
  *fold into an existing code, or register a new one* — is surfaced for a
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
  parser and AST are likewise treated as closed *within an increment*. Most
  increments are type-checker, compiler-emission, VM-opcode-arm and CLI work over
  already-parsed nodes, and an increment never silently grows the grammar or the
  enum. If a feature genuinely needs a node, follow `extending-the-grammar`; if it
  needs an opcode, follow `adding-an-opcode`. An increment that was not authorised
  to do that work still stops and proposes first — growth is sanctioned and
  logged, never casual and never silent. (`TokenKind` follows the `OpCode`
  rule.)
- **The strict DAG holds.** `Grob.Core` is the only shared ground.
  `Grob.Compiler` references `Grob.Core`, never `Grob.Vm`. `Grob.Vm` references
  `Grob.Core` and `Grob.Runtime`, never `Grob.Compiler`. `Grob.Cli` is the
  single composition root — it references every `src/` assembly and nothing
  references it back.
- **C# 14 / .NET 10 (D-310).** `LangVersion 14` is canonical, from the first
  line.
- **Test routing (§3.5).** Each test goes to the project matching its kind —
  `Grob.Compiler.Tests`, `Grob.Vm.Tests`, `Grob.Integration.Tests` and so on.
- **Coverage.** 90% line coverage is a hard bar. `[ExcludeFromCodeCoverage]`
  requires a substantive `Justification`.

## Quality gates

- **Husky.NET pre-push** runs build, test and format verification before every
  push.
- **CodeRabbit** is the in-loop pre-PR reviewer (a deliberate step before the
  PR is opened). There is no Claude reviewer subagent.
- **SonarCloud** runs in CI via the `dotnet-sonarscanner` workflow
  (`.github/workflows/sonarcloud.yml`) on push and pull request — not a NuGet
  analyzer package. `TreatWarningsAsErrors` in `Directory.Build.props` enforces
  the SDK Roslyn analyzers and the `NuGetAudit` advisory gate (D-317).
  **CodeQL** runs in CI via GitHub Advanced Security default setup (too slow for
  per-push local execution); there is no `codeql.yml` and none should be added.
- **External QA cold-read.** At sprint close, the QA brief is the instruction
  file for an in-repo **GPT-5.3 Codex** (Codex CLI) run against the merged
  branch — a different model family, to catch correlated blind spots. This is
  not a Claude task.

## Model policy

- **Sonnet 4.6 (High)** is the default session workhorse for code generation —
  the settled, rules-inlined transcription work.
- **Opus 4.8** is reserved for named structural sub-problems, gated behind
  "only if *this specific thing* gets fiddly", never "this part is hard". It is
  reached via an Opus-pinned subagent under `.claude/agents/`, invoked for the
  named sub-task only.
- **Haiku** for genuinely mechanical, self-contained arms.

## Output and prose conventions

- Diagnostics and errors to **stderr**; results to **stdout**. Quiet on success,
  clear on failure. **Never emoji** in compiler or CLI output.
- Design-doc deliverables are **complete files**, never diffs. Code is edited in
  place in the working tree.
- **British English** in documentation; **Oxford comma never**; never the word
  "simply".
- Decisions-log entries follow ADR style (`D-###`, `Area`, `Supersedes`,
  `Superseded by`), updated in **four-location lockstep** where applicable:
  summary index row, full entry, status table, footer changelog.

## Repo harness map (D-314)

- `CLAUDE.md` — this file, durable project memory.
- `.claude/commands/sprint-N-*.md` — invokable increment prompts (`/sprint-4-a`…).
- `.claude/agents/*.md` — subagents (e.g. the Opus lowering specialist).
- `prompts/<sprint>/` — kickoff records, QA briefs and archive copies of the
  increment commands. The retired `.github/` Copilot harness lives here in
  history for the sprints it drove.
- `docs/design/` — the working design corpus; the decisions log is the
  authority. `docs/wiki/` is a published reference rendering.
