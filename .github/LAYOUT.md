# `.github/` — Grob Copilot customisations

This directory contains everything that customises GitHub Copilot's behaviour
for the Grob repository. Files here are loaded automatically by VS Code,
Visual Studio, Copilot CLI and Copilot cloud agent.

## Layout

```
.github/
├── copilot-instructions.md           Always-on baseline. Auto-loaded for
│                                     every Copilot chat in this repo.
│
├── instructions/                     Path-scoped instructions. Auto-applied
│   │                                 when a matching file is in context.
│   ├── csharp.instructions.md          applyTo: **/*.cs
│   ├── tests.instructions.md           applyTo: **/*Tests*.cs
│   ├── commits.instructions.md         applyTo: **
│   └── project-layout.instructions.md  applyTo: **
│
├── prompts/                          On-demand slash commands. Invoke from
│   │                                 chat with /<name>.
│   ├── sprint-plan.prompt.md           /sprint-plan
│   ├── propose-change.prompt.md        /propose-change
│   ├── model-select.prompt.md          /model-select
│   ├── commit-message.prompt.md        /commit-message
│   └── start-branch.prompt.md          /start-branch
│
├── agents/                           Custom agent personas. Switch with
│   │                                 @<name> in chat.
│   ├── grob-implementer.agent.md       @grob-implementer
│   └── grob-reviewer.agent.md          @grob-reviewer
│
└── skills/                           Auto-activating capabilities. Loaded
    │                                 by Copilot when relevant.
    ├── tdd-cycle/SKILL.md
    ├── trunk-flow/SKILL.md
    └── grob-spec-lookup/SKILL.md
```

## What is what

- **`copilot-instructions.md`** is the always-on baseline. Every Copilot
  chat in this repo loads it as part of the system prompt. Keep it
  concise — it costs tokens on every interaction.
- **`instructions/*.instructions.md`** are loaded only when a file matching
  the `applyTo:` glob is in context. Use them for rules that only apply to
  specific file types.
- **`prompts/*.prompt.md`** are loaded only when explicitly invoked with
  `/<name>` in chat. Use them for repeatable workflows that don't apply
  automatically.
- **`agents/*.agent.md`** are personas with their own tool restrictions and
  default behaviour. Switch personas with `@<name>` in chat.
- **`skills/<name>/SKILL.md`** are task-scoped capabilities. Copilot reads
  the description automatically; if it decides the skill is relevant to
  the current task, it loads the SKILL.md body. Skills can also be invoked
  explicitly with `/<skill-name>`.

## Quick reference

| When you want to… | Do this |
| --- | --- |
| Start a new piece of work | `/start-branch` |
| Plan a Sprint's TDD cycles | `/sprint-plan` |
| Propose a non-trivial change | `/propose-change` |
| Decide which model tier to use | `/model-select` |
| Generate a commit message | `/commit-message` |
| Switch to the implementation agent | `@grob-implementer` |
| Switch to the reviewer agent (before requesting Chris's review) | `@grob-reviewer` |

## Maintenance

- Files here are part of the repository and version-controlled like any
  other source. Changes go through the same trunk-based-development
  process: branch, propose, agree, commit.
- The `applyTo` globs in path-scoped instructions are matched against the
  files Copilot has in context, not the file you're currently editing.
  Test changes by checking the References section in chat replies.
- Skills auto-activate based on their description. If a skill isn't
  triggering when you expect, the description likely needs more
  keyword-rich wording — see GitHub's agent-skills documentation.

## Authority

This directory configures Copilot's behaviour, but it does not override
the design corpus. When `copilot-instructions.md` or any other file here
says one thing and `docs/design/grob-decisions-log.md` says another, the
decisions log wins. Surface the drift rather than silently following one
source.
