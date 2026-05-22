# Grob — GitHub Copilot agentic setup

This directory configures GitHub Copilot as a first-class coding partner for Grob. It
is not a single instructions file — it is a layered system: always-on instructions,
path-scoped rules, custom agents, reusable skills, on-demand task prompts, and MCP
server connections. Together they give Copilot the context to work inside Grob's
architecture without being told the invariants every time.

The decisions log (`docs/design/grob-decisions-log.md`) is the authority for the
project; everything here defers to it.

## The layers and how they stack

When Copilot works on a file, it assembles context from several layers. Highest
priority to lowest, where they conflict:

1. **Personal instructions** (your VS Code / GitHub profile — not in this repo).
2. **Path-scoped rules** — `.github/instructions/*.instructions.md`, applied
   automatically when the file you are editing matches their `applyTo` glob.
3. **Repository instructions** — `.github/copilot-instructions.md`, always on.
4. **`AGENTS.md`** at the repo root, if present (recognised by Copilot and other
   agents).

On top of that, two opt-in layers you invoke deliberately:

- **Custom agents** (`.github/agents/*.agent.md`) — personas you select from the agent
  picker or assign to an issue. Each carries a focused brief and tool set.
- **Prompt files** (`.github/prompts/*.prompt.md`) — reusable task runners invoked with
  `/name` in chat.

And one layer agents discover automatically:

- **Skills** (`.github/skills/*/SKILL.md`) — task-specific procedures any agent can pull
  in when the task matches.

## What is here

```
.github/
  copilot-instructions.md              Always-on foundation: identity, DAG, invariants, tone
  instructions/
    csharp-host.instructions.md        **/*.cs        — architecture, GrobValue, opcodes, tests
    grob-lang.instructions.md          **/*.grob      — language syntax and idioms
    tests.instructions.md              tests/**/*.cs  — the five test projects, gold masters
    plugins.instructions.md            plugins/**/*.cs — IGrobPlugin authoring
  agents/
    grob-compiler-engineer.agent.md    Implements/reviews the C# host code
    grob-sprint-implementer.agent.md   One scoped sprint task → green, reviewable changeset
    grob-design-reviewer.agent.md      Read-only consistency review against the log
  prompts/
    sprint-1-kickoff.prompt.md         /sprint-1-kickoff — scaffold + lexer + parser
    start-sprint-task.prompt.md        /start-sprint-task — one scoped task, end to end
    consistency-review.prompt.md       /consistency-review — adversarial corpus review
  skills/
    adding-an-opcode/SKILL.md          Enum + compiler + VM, together, with stability rules
    authoring-a-plugin/SKILL.md        IGrobPlugin end to end
    writing-an-error-test/SKILL.md     Gold-master error pairs + the regeneration discipline
    adding-a-stdlib-function/SKILL.md  Module vs method, one-way principle, formatAs boundary
    logging-a-decision/SKILL.md        ADR-style entry, index + changelog + supersession links
.vscode/
  mcp.json                             Microsoft Learn + GitHub MCP servers (VS Code)
```

The path-scoped split is the thing that makes this fit Grob specifically: Grob is two
languages in one repo — the C# that implements it and the `.grob` programs written in
it. The `applyTo` globs route the right rules to each automatically, so Copilot writes
idiomatic `:=`/backtick-path Grob in `.grob` files and ordinary modern C# in `.cs`
files, and never confuses the two.

## MCP servers

Two are configured, both chosen because they earn their place for a Windows-native
.NET compiler project:

- **Microsoft Learn** (`https://learn.microsoft.com/api/mcp`) — GA, no authentication,
  streamable HTTP. The authoritative, current source for the .NET 10 BCL, BenchmarkDotNet,
  OmniSharp LSP, Azure and the rest of the Microsoft-flavoured stack Grob depends on.
  This is the single highest-value connector for this project: it stops the agent from
  guessing API signatures.
- **GitHub** (`https://api.githubcopilot.com/mcp/`) — issues, PRs and repository
  operations, used as needed; central if you later delegate work to the cloud agent.

Context7 and other general doc servers were considered and left out: Learn already
covers the Microsoft stack that is almost all of Grob's dependency surface, and a third
server is noise until a non-Microsoft dependency needs it.

### Enabling MCP — manual steps

**VS Code (local agent mode — your primary surface):** `.vscode/mcp.json` is committed
and picked up automatically. Microsoft Learn needs nothing. The GitHub server uses VS
Code's built-in GitHub sign-in (OAuth) — no personal access token to mint; VS Code
prompts you to authorise on first use. Confirm both are running via the MCP servers list
in the chat panel. (Microsoft Learn returns `405` if opened in a browser — that is
expected; it is an MCP endpoint, not a web page.)

**GitHub cloud agent (delegated PRs):** repo-level MCP for the cloud agent is **not** a
committed file — it is entered in repository settings. Repository → Settings → Copilot →
Coding agent → MCP configuration, as JSON using an `mcpServers` object:

```json
{
  "mcpServers": {
    "microsoft-docs": {
      "type": "http",
      "url": "https://learn.microsoft.com/api/mcp",
      "tools": ["*"]
    }
  }
}
```

The GitHub MCP server is built in for the cloud agent and Copilot CLI — you do not need
to add it. If you later add a server that needs a secret, store it as a repository
Actions secret prefixed `COPILOT_MCP_` and reference it in the config; only that prefix
is exposed to the MCP configuration. The cloud agent does not currently support remote
MCP servers that use OAuth.

## Other one-time enablement

- **Instruction files** are on by default in current VS Code. If path-scoped rules are
  not applying, check `chat.includeApplyingInstructions` and that the
  `.github/instructions/` location is enabled in `chat.instructionsFilesLocations`.
- **Custom agents** appear in the agent picker once the `.agent.md` files are on the
  default branch. For the cloud agent, refresh the agents tab after merging.
- **Prompt files** are invoked with `/sprint-1-kickoff`, `/start-sprint-task`,
  `/consistency-review` in chat.

## How to drive it day to day

- Starting the next piece of work: `/start-sprint-task` and describe the task. It locates
  the sprint and acceptance criterion, pulls the right skill, proposes an increment
  breakdown, then implements step by step with tests, checkpointing for review.
- Beginning implementation proper (after clox): `/sprint-1-kickoff`. It scaffolds the
  solution against the locked architecture and builds the lexer and error-recovering
  parser.
- Checking the corpus or a change for drift: `/consistency-review`. Read-only; reports
  findings ranked by severity, does not resolve them.
- A focused job with a persona: pick **Grob Compiler Engineer** for host-code work,
  **Grob Sprint Implementer** to carry one scoped task to a green, reviewable changeset
  in-session, **Grob Design Reviewer** for a read-only audit.

### Model guidance

Following the project's established session pattern: reach for the more capable model on
work that turns on design judgement (the error-recovering parser, design review, closing
an open question) and the efficient model on mechanical, well-specified work (scaffolds,
boilerplate, straightforward stdlib additions). The prompt files note this per task.

## Keeping this current

These files encode decisions. When a decision changes, the affected instruction, skill
or agent text should change with it — the same way the spec docs do. If you find a rule
here that contradicts the decisions log, the log wins and the rule here is the thing to
fix. Treat that as a finding, not a silent edit.
