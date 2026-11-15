# Grob — Agentic Development Strategy

> **Purpose:** This document defines how Grob becomes a first-class citizen for
> agentic development — what an AI harness needs from a language toolchain to treat
> that language as native, what Grob builds to supply it and in what order. Grob's
> purpose does not change: it is a statically typed scripting language for Windows
> developers and sysadmins. What changes is that the toolchain now has two readers.
>
> **Authority:** D-434 adopts this strategy. D-435 sets the security posture it
> depends on, superseding D-072. D-436 makes the capability seam a gate on Sprint
> 9C. The decisions log remains the authority on conflict.
>
> **Status:** Planning. One item is time-critical — the seam gate on Sprint 9C
> (§7). The v1 deliverables land in Sprint 12. The rest is post-v1 and waits on
> the open questions named in each section.
>
> **Last updated:** November 2026

-----

## Table of Contents

1. [The Position](#1-the-position)
2. [What Agent-Native Means](#2-what-agent-native-means)
3. [The Problem Grob Starts With](#3-the-problem-grob-starts-with)
4. [The Landscape — November 2026](#4-the-landscape--november-2026)
5. [Track 1 — The Compiler as Teacher](#5-track-1--the-compiler-as-teacher)
6. [Track 2 — The Context Kit](#6-track-2--the-context-kit)
7. [Track 3 — The Capability Seam](#7-track-3--the-capability-seam)
8. [Track 4 — The Benchmark](#8-track-4--the-benchmark)
9. [Track 5 — Restricted Runs and Embedding](#9-track-5--restricted-runs-and-embedding)
10. [Trust Model](#10-trust-model)
11. [Syntax Policy](#11-syntax-policy)
12. [What Grob Will Not Do](#12-what-grob-will-not-do)
13. [Sequencing](#13-sequencing)
14. [Sources](#14-sources)

-----

## 1. The Position

Grob is the typed scripting language an agent can be trusted to write. Every
mistake is reported in one pass before anything runs. Every effect on the machine
passes through a seam the host controls. The toolchain teaches the language to a
model that has never seen it.

Two authors write Grob from here on:

- **A person**, as today. Nothing in this strategy makes the language worse for
  them. A diagnostic that helps a model recover helps a person recover.
- **An agent**, writing either a script a person will keep or a throwaway script
  it runs once to get a job done. The second case is the bet (§9).

The design target in the project's foundational principle stands. Grob should be a
credible answer to "what should I use for this scripting task?" — and from now on
the one asking is as likely to be a harness as a developer.

-----

## 2. What Agent-Native Means

It does not mean an MCP server, a chat plugin or a model integration. Harnesses
shell out. A language is native to an agent when four things are true:

1. **The agent can learn it in context.** The toolchain carries its own
   documentation, sized for a context window and matched to the installed version.
2. **The agent can correct itself.** Diagnostics are structured, stable and say
   what the correct form is. A loop of write, check and repair converges in a few
   rounds.
3. **The agent's work can be checked before it runs.** Static typing and
   collect-all-errors already give Grob this. It is the property Python lacks and
   the reason the strategy is credible.
4. **The agent's reach can be bounded.** The host decides what a script may touch,
   and can ask what it would do without letting it.

Points 1 and 2 are tooling. Point 3 is the language as designed. Point 4 is runtime
architecture, and it is the expensive one to add late.

-----

## 3. The Problem Grob Starts With

No model has been trained on Grob. A model asked to write it produces C# and Go
habits: `var`, semicolons, `function`, `let`, `null`. The language's readability to
a C# or Go developer — a design goal — is also the source of the interference.

Published work on low-resource and no-resource programming languages points the
same way throughout: performance tracks training data, in-context documentation
and examples are a cheap and dependable gain on large models, examples that
translate from a well-known language are among the most robust forms, and
compiler feedback in an iterative loop recovers a good part of the gap. Fine-tuning
is not available to a project this size and can make large models worse.

That evidence sets the order of work. The kit (§6) and the compiler loop (§5) come
before anything else, because they are what turns a model that has never seen Grob
into one that writes it correctly by the second attempt.

-----

## 4. The Landscape — November 2026

Three developments shape the strategy. Each is recorded as found on 15 November
2026; §14 lists the sources.

**Code as action.** Harnesses increasingly have the model write one short program
that calls tools, in place of a long run of individual tool calls. Anthropic and
Cloudflare published the pattern, and independent reproductions report large
reductions in context use. The scripts are written, run once and discarded. This is
the bet in §9: the language those scripts are written in is an open question, and
today the answer is Python or TypeScript by default.

**Zero.** Vercel Labs released an experimental systems language in May 2026 whose
compiler emits JSON diagnostics with stable codes and repair metadata, whose CLI
serves agent guidance matched to the installed version and whose I/O is
capability-based. It is a systems language, pre-1.0 and unstable. It matters here
because it makes structured diagnostics and a version-matched kit the baseline for
any language claiming this ground. They are necessary for Grob. They are not what
distinguishes it.

**Monty.** Pydantic released an interpreter in February 2026 built only to run
agent-written Python: scripts reach the outside world through functions the host
supplies, and are checked against type stubs before they run. It is the closest
existing thing to §9, built for Python hosts.

**Where that leaves Grob.** The unoccupied ground is typed, checked-before-run
scripting with first-class file system, process and JSON support, for Windows and
.NET hosts. That is the audience Grob already has. The claim that nobody holds this
ground is a judgement, not a measurement, and should be re-tested before any
post-v1 work in §9 is authorised.

-----

## 5. Track 1 — The Compiler as Teacher

**In v1, Sprint 12.** Authority: D-434. Tracked as R-36. Contract: OQ-019.

`grob check` gains a machine-readable mode. The pieces:

- **`grob check --json`.** One document per run: every diagnostic with its code,
  severity, message, file and full source range. The schema is versioned and
  carries the same stability promise as the error codes themselves.
- **Structured help and fixes.** `Diagnostic` today carries a code, a message, a
  range and a severity. It grows `note` and `help` lines — which §10's renderer
  already needs (R-25) — and, where the fix is mechanical, a list of text edits
  with a stated applicability.
- **Foreign-idiom diagnostics.** When the parser meets a construct that is valid
  in a neighbouring language and wrong in Grob, the diagnostic names it and gives
  the Grob form. `var x = 1` is answered with `x := 1`. This is the cheapest
  correction for the interference §3 describes, and it serves a C# developer on
  their first day equally well. Any new code is minted by the increment under
  D-316.
- **`grob --explain Exxxx`.** Already specified. R-10's error documents become
  agent-facing reference as well as human-facing, which raises their priority
  without changing their shape.

**Prerequisites.** R-25 (the §10 renderer), R-26 (source columns) and R-29
(cascades). A machine consumer is less forgiving of a missing column or five
diagnostics for one root cause than a person is. These close before R-36 starts.

**What is not decided.** The schema, whether SARIF is offered alongside it, the
applicability levels, the idiom list and whether runtime errors from `grob run`
share the contract. All are OQ-019.

-----

## 6. Track 2 — The Context Kit

**In v1, Sprint 12.** Authority: D-434. Tracked as R-36. Design: OQ-020.

The installed binary carries what an agent needs to write Grob, so the guidance
never disagrees with the compiler it sits beside.

- **`grob skills`** prints the kit in three parts: a language reference cut to a
  stated token budget; the idioms that differ from C#, Go, PowerShell and Python;
  paired examples that translate a short PowerShell or Python script into Grob.
- **`grob new`** scaffolds an agent section into the project it creates, pointing
  a harness at `grob skills` and `grob check --json`.
- **Every sample in the kit compiles.** The public-samples gate (D-430) extends to
  the kit. A kit that teaches code the compiler rejects is worse than no kit.

The seed exists: `.claude/skills/writing-grob-source` is the project's own
guidance for agents writing `.grob` files, written for contributors. The kit is
its public descendant.

-----

## 7. Track 3 — The Capability Seam

**Now, as a gate on Sprint 9C.** Authority: D-436. Tracked as R-35.

D-319 sketched six capability interfaces and D-343 landed the injection mechanism.
`IStandardStreams`, `IClock`, `IEnvironment` and `IRandomSource` exist. `IFileSystem`
arrives with one member under D-430. `IProcessRunner` does not exist yet. Sprint 9C
builds `fs` and `process` — the two modules through which a script changes the
machine.

If those natives reach the OS through injected capabilities, restricted runs and
plan mode (§9) are an alternate host: the same move the playground and the
error-examples harness already make. If they call `System.IO` and
`System.Diagnostics.Process` directly, §9 becomes a rewrite of the two largest
stdlib modules after they ship. The cost of doing it properly is close to nil in
the sprint that builds them and high in any later one.

D-436 therefore requires it and shapes the interfaces so that each member is one
operation that either reads state or changes it. A source guard keeps it so.

-----

## 8. Track 4 — The Benchmark

**After Sprint 12.** Authority: D-434. Tracked as R-37. Design: OQ-021.

"First-class for agents" is a claim. The benchmark is what makes it a measurement.

- A fixed set of tasks drawn from the four use cases that drive Grob's design:
  Azure CLI and Bicep scripting, SharePoint PnP wrapping, Azure DevOps REST tools
  and agent hook tools.
- Each task is given to a harness with the kit and nothing else. Scored on two
  numbers: correct on the first attempt, and correct within three rounds of
  `grob check --json`.
- Run against more than one harness and model, on a recorded date, with results
  committed.

The benchmark has a second job. It is the evidence base for §11. Where agents fail
repeatedly on the same construct, that is information about the language, and it
is the only kind of information that authorises a syntax change on these grounds.

-----

## 9. Track 5 — Restricted Runs and Embedding

**Post-v1.** Authority: D-435. Tracked as R-38. Design: OQ-022, OQ-023, OQ-014.

This is the bet on a pattern that is not yet mainstream: an agent writing a
throwaway Grob script as its way of acting. Three pieces, in order:

1. **Restricted run.** A run in which the host grants capabilities explicitly and
   a call needing one that was not granted fails. Read the file system but not
   write it; run `az` but nothing else. The grant surface is OQ-022.
2. **Plan mode.** A run over a recording host: the script executes and reads
   return real data, but each state change is recorded and reported in place of
   being performed. A person or a harness reviews what the script would do before
   allowing it. Semantics are OQ-022.
3. **Embedding.** A published host package so a .NET agent host runs Grob
   in-process with its own natives, its own capability set and resource limits.
   The playground (D-319) is the first embedding host; this makes the route
   public. OQ-023. `Grob.Mcp` (OQ-014) adds typed bindings generated from MCP tool
   schemas, so a script calls a tool as a typed function.

None of this is authorised for build. §4's judgement is re-tested first.

-----

## 10. Trust Model

D-435 replaces D-072's single posture with two modes.

| | Trusted run | Restricted run |
|---|---|---|
| Who wrote the script | Someone the person running it trusts, usually themselves | Not trusted to stay in bounds — typically an agent |
| Default | Yes, in v1 and after | No — opt-in, post-v1 |
| Standard library | Full | Only the capabilities the host granted |
| Plugins | Loaded as today; arbitrary code (D-073) | Refused unless explicitly granted |
| The claim | Trust the author, document the risks, make the safe path the obvious one | A capability-gated standard library |

**The limit of the claim.** A restricted run is not a sandbox and is never called
one. It bounds what the standard library will do on a script's behalf. It is not
an isolation boundary against code written to attack the runtime, and it gives no
protection once a plugin is granted, because a plugin is arbitrary .NET code. A
host that needs isolation runs Grob inside a container or an OS sandbox as well.

-----

## 11. Syntax Policy

Grob's syntax is not changed to suit agents on the strength of an argument. It is
changed on the strength of evidence, and only after a cheaper remedy has failed.

1. **No change before the benchmark.** Until §8 exists there is no evidence of
   where agents fail, only expectation.
2. **A diagnostic before a grammar change.** If agents write `var`, the first
   answer is a diagnostic that teaches `:=` (§5). If the benchmark then shows the
   loop converging, the syntax stays.
3. **Identity is a cost.** `:=`, no semicolons, backtick raw strings and the
   `select` and switch split are what make Grob read as Grob. A change that
   trades them for a few points of first-attempt success needs to say what it
   costs a person reading the language.
4. **A change is a logged decision**, argued on its merits like any other.

-----

## 12. What Grob Will Not Do

- **No model in the toolchain.** `grob` never calls an LLM. `Grob.Llm` (OQ-013)
  is a plugin a script imports, not part of the compiler.
- **No sandbox claim.** §10.
- **No second language.** There is no agent dialect, no relaxed mode and no
  syntax a person would not write.
- **No fix applied silently.** A structured fix is offered. `grob check` never
  rewrites a file.
- **No telemetry** to learn how agents use Grob. The benchmark is the instrument.

-----

## 13. Sequencing

| When | What | Tracked |
|---|---|---|
| Sprint 9C | `fs` and `process` natives through the capability seam | R-35 |
| Before Sprint 12 | OQ-019 and OQ-020 resolved; R-25, R-26 and R-29 closed | — |
| Sprint 12 | `grob check --json`, structured fixes, foreign-idiom diagnostics, `grob skills` | R-36 |
| After Sprint 12 | The benchmark designed (OQ-021), built and run once | R-37 |
| Post-v1 | Restricted run and plan mode (OQ-022) | R-38 |
| Post-v1 | Embedding host package (OQ-023); `Grob.Mcp` typed bindings (OQ-014) | — |

Grob has no published delivery date (D-433). These items slip with their sprint;
they are not cut.

-----

## 14. Sources

Read on 15 November 2026. Web sources are context, not authority, and will age.

- Anthropic — *Code execution with MCP: building more efficient AI agents*,
  <https://www.anthropic.com/engineering/code-execution-with-mcp>
- Cloudflare — *Code Mode: the better way to use MCP*,
  <https://blog.cloudflare.com/code-mode/>
- Pydantic — *Your agent would rather write code*,
  <https://pydantic.dev/articles/your-agent-would-rather-write-code>
- Pydantic — *Pydantic Monty: you probably don't need a full sandbox*,
  <https://pydantic.dev/articles/pydantic-monty>
- Vercel Labs — Zero, <https://github.com/vercel-labs/zero>; coverage at
  <https://techstrong.ai/features/vercel-labs-builds-a-programming-language-designed-for-ai-agents/>
  and <https://betterstack.com/community/guides/ai/zero-language/>
- *No Resource, No Benchmarks, No Problem? Evaluating and Improving LLMs for Code
  Generation in No-Resource Languages*, <https://arxiv.org/pdf/2606.16827>
- *Enhancing Code Generation for Low-Resource Languages: No Silver Bullet*,
  arXiv 2501.19085
- *In-Context Learning for Esoteric Programming Languages*, NeurIPS 2025,
  <https://neurips.cc/virtual/2025/131647>
- A study of GPT-5 on the Idris Exercism track with compiler-guided refinement,
  <https://arxiv.org/pdf/2602.11481>

-----

*Created November 2026 — D-434, D-435, D-436.*
