---
description: Decide which Claude model (Haiku, Sonnet or Opus) fits the work in front of you, and explain the reasoning.
argument-hint: [task in a phrase]
---

# Model selection

In Claude Code there is one subscription and three models, switched with `/model`.
There are no separate billing tiers, no BYOK, no local model — the old three-provider
ladder is gone. Pick the cheapest model that will do the job *well*. "Well" is the
operative word — cheap-and-bad is not cheap, it is wasted time.

The task to assess is **$ARGUMENTS**.

## The three models

### Haiku — mechanical work

Fast and cheap. For work where the shape is fully dictated by something external — a
method signature, the allowlist in a spec, an existing pattern to mirror. Boilerplate,
doc-comment first drafts, commit-message drafting, test stubs from a signature,
single-file mechanical edits with no judgement call. The kind of self-contained arm you
could describe in one sentence with a clear template.

**Reach for it when:** the deliverable is "do the obvious thing" and there is a clear
template to follow. If the first attempt produces nonsense, escalate to Sonnet.

### Sonnet — the default

The workhorse. Routine reasoning where the *design is already settled*: writing tests
against a clear spec, implementing an agreed proposal cycle by cycle, refactoring within
a file, explaining what an error means, fixing a localised bug. Almost all Grob
implementation work is Sonnet work — including the compile-time-constant evaluators,
the typed-opcode selection, the recovery machinery, anything where the spec or an inline
reference block already fixes the shape.

**Reach for it when:** the work requires real reasoning but you are translating intent
to code, not deciding what the intent should be. This is the default; start here unless
a trigger below applies.

### Opus — load-bearing design

The strongest model, for the named structural triggers only. Reserve it for work where
"approximately right" is not good enough and the cost of being wrong exceeds the cost of
the inference:

- **Multi-file architectural reasoning** — a change whose correctness depends on how
  three or more files fit together.
- **Design proposals where the alternatives are not obvious** — you would want a second
  pair of eyes before committing to a direction.
- **Debugging cascading failures** — a bug that resists the first three explanations you
  can think of.
- **The specific structural sub-problems an increment prompt flags for Opus** — e.g. a
  mechanism explicitly shaped so later sprints extend rather than duplicate it. The
  trigger is "later sprints build on this shape", not "this part is hard".
- **Adversarial review** — `grob-reviewer` and `grob-design-reviewer` default to Opus,
  because a missed finding is the expensive failure mode.

**Reach for it when:** the work is genuinely "decide what to do", not "do the thing".

## Decision questions

Answer in order. Stop at the first "yes":

1. **Is the deliverable a string of tokens with a clear template?** (Commit message,
   doc comment, test stub from a signature, boilerplate, obvious wiring.)
   → **Haiku.**
2. **Is the design settled, and the work is implementing it?** (Translating an agreed
   proposal to code, writing tests for a clear spec, refactoring a single file, fixing a
   localised bug.)
   → **Sonnet.**
3. **Does the work require choosing between non-obvious alternatives, reasoning across
   multiple files, or debugging something that resists explanation — or did the
   increment prompt name this part as an Opus trigger?**
   → **Opus.**
4. **If you cannot decide,** start one model lower than your first instinct and escalate
   if it struggles. Most people default too high.
5. **If genuinely stuck, ask the maintainer.** Do not escalate silently.

## Cost-conscious patterns

- **Plan with Opus, execute with Sonnet, scaffold with Haiku.** Opus produces the
  proposal; Sonnet implements it cycle by cycle; Haiku fills in the obvious bits. The
  same pattern works at every scale.
- **Keep prompt runs interactive.** Interactive Claude Code sessions draw on the flat
  subscription windows; headless `claude -p` runs meter separately. Drive increments
  interactively.
- **Do not use Opus for refactor steps.** A behaviour-preserving refactor is mechanical
  by definition — Haiku or Sonnet.
- **Do not use Haiku for the type checker.** Sustained type-checking reasoning is Sonnet
  work at least, Opus where the increment prompt says so.
- **Switch in place with `/model`.** No re-tooling, no provider change — escalate or
  drop back mid-session as the work shifts.

## How to answer

When invoked with a specific task, produce:

```
# Model selection: <task in one phrase>

**Recommendation:** <Haiku | Sonnet | Opus>.

**Reasoning:** <one or two sentences naming which decision question triggered it, and
what that means for the work.>

**Watch for:** <one signal that would tell you to escalate. for Haiku: "if the first
attempt produces nonsense, switch to Sonnet." for Sonnet: "if correctness turns on more
than two files fitting together, switch to Opus.">
```
