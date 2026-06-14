# Grob — Personality & Identity

> This document defines Grob’s character. It informs error messages, documentation
> tone, the REPL experience, CLI output, community culture, and future tooling.
> Sparky is not decoration — Sparky is this personality made visible.
> Update with dated entries as the character develops.

-----

## Foundational Principle

Grob is a hobby project but it is not a toy. Learning is a byproduct, not the
primary purpose. Grob is a serious attempt to build a genuine scripting language.
It should be designed, documented, and built with that in mind.

When there is a choice between the approach that teaches more and the approach
that produces a better language — choose the better language. The learning will follow.

**The design target:** Grob should be able to stand next to Go, PowerShell, and
Python as a credible answer to the question “what should I use for this scripting
task?” Each of those languages has real, well-documented weaknesses in the scripting
space. Go is too ceremonious for scripts. PowerShell’s syntax is hostile. Python is
dynamically typed and increasingly clunky at scale. Nobody has solved this cleanly.
That gap is what Grob is designed to fill.

The origin as a hobby project is irrelevant to whether it can fill that gap.
The design decisions are what matter — and they should be serious ones, made for
the right reasons, every time.

-----

## The Core Character

Grob has one character expressed in three modes depending on context:

|Context                         |Mode                |Feel                                                       |
|--------------------------------|--------------------|-----------------------------------------------------------|
|You’re stuck, something’s broken|Seasoned engineer   |Calm, precise, tells you what went wrong and why — no panic|
|You’re learning, exploring      |Enthusiastic teacher|Genuinely wants you to succeed, meets you where you are    |
|You’re building, in the zone    |Sparky the raccoon  |Gets out of your way, curious, scrappy, figures things out |

These aren’t three different personalities. They’re the same character reading the room.

-----

## The Personality Statement

> Grob is helpful without being patronising, opinionated without being dogmatic,
> and precise without being cold. It celebrates your wins, explains your mistakes,
> and gets out of your way when you know what you’re doing.

-----

## How This Plays Out

### Error messages

Grob’s compiler found a problem. The job of the error message is not to report
a failure — it’s to unblock the developer.

**Principles:**

- Say what went wrong, where, and why — in that order
- Never blame the developer, never be cryptic
- Suggest the fix when it’s obvious. Don’t suggest it when it isn’t — guessing
  is worse than silence
- Every error reported clearly with full context — no walls of noise, no errors hidden

**Tone:** helpful and direct. Not apologetic, not terse.

**Example — type mismatch:**

```
error[E0001]: type mismatch
  --> deploy.grob:14:12
   |
14 |     count := "hello" + 42
   |              ^^^^^^^^^^^^
   |
   = expected: string + string, or int + int
   = got: string + int
   = help: use string interpolation: "${count}${42}"
```

**Not this:**

```
ERROR: type mismatch at line 14
```

**Not this either:**

```
Oops! It looks like there might be a little type problem here! 😊
```

Helpful and direct. Warm tone, no waffle.

-----

### First run

The first time a Grob script runs successfully, Grob notices.

Not a party. Not confetti. A quiet acknowledgement that something just started.

```
G> grob run hello.grob
Hello, World!

  ✦ First script. Nice work.
```

After the first run, it never says it again. It happened, it was noted, we move on.
The celebration is in the acknowledgement, not the repetition.

-----

### The REPL

The REPL prompt is `G>` — the same mark as the logo. This is deliberate.
Every line typed in the REPL is Grob executing. The prompt should feel like
an invitation, not a cursor.

```
Grob 1.0.0  |  type 'help' for guidance, 'exit' to quit

G> _
```

The REPL is where beginners will spend most of their early time. It should
feel like a workbench — somewhere you can try things without consequence.

**REPL personality specifics:**

- Multiline input is handled gracefully — trailing operator or open brace
  signals continuation without requiring a special character
- Errors in the REPL don’t end the session — they explain and continue
- `help` gives real help, not a wall of syntax
- `exit` works. So does Ctrl+C. Don’t make people guess.

-----

### CLI output

The CLI (`grob run`, `grob check`, `grob repl`) should feel like a well-made
Unix tool — quiet when things work, clear when they don’t.

**Principles:**

- No output on success unless there’s something to say
- Errors go to stderr, results go to stdout — pipeline-friendly always
- `--verbose` is available but never the default
- Progress indicators for operations that take time (compilation of large scripts,
  plugin loading) — a spinner, not silence

**`grob check` output (no errors):**

```
✓ stale-branches.grob
```

**`grob check` output (with errors):**

```
✗ stale-branches.grob

  Line 14 — type error
  ...
```

No noise. No “compilation successful in 3ms.” If it worked, the tick says so.

-----

### `--help` output

`--help` should be the thing you actually read, not the thing you Google past.

Written in plain English. Examples included. Not exhaustive — focused on what
someone reaching for `--help` actually needs.

```
grob — a statically typed scripting language

Usage:
  grob run <script>          Run a Grob script
  grob repl                  Start an interactive session
  grob check <script>        Type-check without running
  grob fmt <script>          Format a Grob script
  grob new <script>          Create a new script with boilerplate
  grob install <plugin>      Install a plugin from NuGet
  grob search <query>        Search for available plugins

Options:
  --dev-plugin <path>        Load a plugin (.dll) for development
  --verbose                  Show compilation details
  --version                  Print version and exit

Examples:
  grob run deploy.grob
  grob run stale-branches.grob --params stale.grobparams
  grob run report.grob --dev-plugin Grob.Http.dll

G> help anytime.
```

The last line is a small touch. It’s Sparky. It’s consistent with the character.
It doesn’t overstay its welcome.

**Bare `grob`.** Running `grob` with no command prints this same listing and exits 0.
Typing the name should teach you the tool, not trap you — so it never drops into the
REPL. `grob repl` is the only way into an interactive session, on purpose. This matches
what the audience already expects from git, go, dotnet and winget: type the tool’s name,
see what it can do (D-312).

-----

## Opinionated Defaults

Grob has opinions. It doesn’t enforce them at gunpoint.

|Area            |Default opinion                         |Enforcement                   |
|----------------|----------------------------------------|------------------------------|
|Naming          |`snake_case` idiomatic — see `grob-formatter-specification.md`|None (formatter, not compiler)|
|Nil handling    |Explicit — no zero values, no silent nil|Compiler error                |
|Unused variables|Worth knowing about                     |Compiler warning              |
|Unused imports  |Worth knowing about                     |Compiler warning              |
|Line length     |No opinion                              |None                          |
|Formatting      |`grob fmt` formats your code            |Never automatic, always opt-in|

Warnings are warnings. They don’t block execution. They’re Grob saying
*“you might want to look at this”* — not *“I won’t run until you fix it.”*

Errors are errors. Nil safety and type correctness are non-negotiable.
The compiler is strict where it matters and quiet where it doesn’t.

**Why no compiler warning on naming.** Grob scripts interoperate with
real external systems whose field names follow upstream conventions.
Azure ARM returns camelCase. GitHub returns snake_case. HTTP headers
are kebab-case. SharePoint fields are PascalCase. A script talking to
any of these will end up with identifiers that match the upstream
contract, not an internal Grob opinion. A compiler warning that fired
on every `subscriptionId` or `resourceGroup` would train users to mute
warnings — and muted warnings are worse than no warnings when a
genuinely useful one (shadowing, unused import) fires later. Style is
a formatter concern and a documentation concern; the compiler reserves
warnings for correctness-adjacent issues.

-----

## What Grob Never Does

- Never outputs emoji in compiler errors or CLI output (the REPL may, sparingly)
- Never uses the word “simply” in documentation or error messages
- Never produces a wall of text when a sentence will do
- Never silently swallows an error
- Never makes the developer feel stupid for making a mistake
- Never celebrates routine operations — only genuinely notable moments

-----

## The AI Tutor — Deferred Idea

*Captured here for future design. Not in scope for the initial release.*

The vision: a guided learning companion built alongside the language. Not a
chatbot bolted on — a structured learning path that grows with the language.
Think SharpBASIC’s learning path but at full scale.

Lessons would build on each other. The tutor knows what you’ve already learned.
It gives you just enough to take the next step, then gets out of the way.

The personality of the tutor is Grob’s personality — it’s Sparky in teacher mode.
Same character, different context.

This idea lives here until Grob is mature enough to make it real.
When that time comes, start a dedicated design session.

-----

## Sparky — Character Reference

**Name:** Sparky
**Species:** Raccoon
**Visual reference:** `Sparky___Character_Sheet_v1.png`

Sparky is Grob’s personality made visible. The same character principles apply:

|Sparky’s trait    |How it shows in the language                        |
|------------------|----------------------------------------------------|
|Curious           |Error messages that explain, not just report        |
|Scrappy builder   |Gets out of your way when you know what you’re doing|
|Figures things out|Opinionated defaults, not rigid rules               |
|Celebrates wins   |First run acknowledgement, REPL warmth              |
|Approachable      |`--help` you actually read, docs you don’t dread    |

The `G>` mark is shared between Sparky and the REPL prompt. That’s not a
coincidence — it’s the same thing. Every time someone types at the REPL they’re
in Sparky’s world.

-----

*Last updated: June 2026 — D-312: bare `grob` (no command) prints the `--help`*
*listing to stdout and exits 0; it never launches the REPL — `grob repl` is the*
*sole REPL entrance. Statement added to the `--help` output section.*
*Previous: April 2026 — Session E: stale diagnostic example in Error messages*
*section replaced with canonical rustc-style `error[Exxxx]:` format per*
*`grob-v1-requirements.md` §10. (Earlier revision of this changelog also*
*cited ADR-0017; the format itself is specified in §10. ADR-0014 governs*
*error code numbering and ADR-0017 governs error code stability — neither*
*ADR specifies the message-display format.) Opinionated Defaults Naming*
*row verified as already updated (D-283 applied in prior session).*
*Previous: April 2026*
*Update this document when the character develops or new surfaces are designed.*
*This document and the character sheet are the two halves of the same decision.*
