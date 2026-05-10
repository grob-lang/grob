---
mode: 'ask'
description: 'Decide which model tier (local Qwen, Copilot Sonnet, BYOK Opus) is right for the work in front of you and explain the reasoning.'
---

# Model selection

Three tiers are available. Pick the cheapest that will do the job *well*.
"Well" is the operative word — cheap-and-bad is not cheap, it's wasted time.

## The tiers

### Tier 1: Local Qwen2.5-Coder via Ollama

**Cost:** £0. Runs on the GTX 1060.
**Latency:** ~15–25 tokens/sec, slower than cloud but no rate limits.
**Strengths:** Mechanical work where the shape is dictated by something
external — a method signature, a spec, an existing pattern. Fill-in-middle,
boilerplate, doc comment first drafts, commit message generation, test
stubs from a signature.
**Weaknesses:** Multi-file reasoning, novel design, debugging across
abstraction boundaries, anything where the answer is genuinely unclear.

**Reach for it when:** the work is "do the obvious thing", you can describe
the deliverable in a sentence and there's a clear template to follow.

### Tier 2: Copilot native Sonnet 4.6

**Cost:** Consumes the $39/month AI Credit allowance. Rate is $3/M input,
$15/M output. A routine TDD cycle costs roughly £0.05–0.15.
**Latency:** Fast, hosted, full integration with VS Code agent mode.
**Strengths:** Routine reasoning — writing tests against a clear spec,
explaining what an error means, refactoring within a file, generating
implementation from a proposal that's already been agreed.
**Weaknesses:** Same model as Tier 3 (Sonnet 4.6), but the $39 budget
constrains how long sessions can run before you have to top up.

**Reach for it when:** the work requires real reasoning but the *design*
is already settled. You're translating intent to code, not deciding what
the intent should be.

### Tier 3: Anthropic API via BYOK — Opus 4.7

**Cost:** $5/M input, $25/M output, billed directly by Anthropic. A heavy
design session can run to £2–5. Hard caps configurable on the Anthropic side.
**Latency:** Slower than Sonnet by ~30%. Worth it when the answer matters.
**Strengths:** Multi-file architectural reasoning, debugging cascading
failures, design proposals where the alternatives aren't obvious, anything
where "approximately right" isn't good enough.
**Weaknesses:** Expensive. Reserve it for problems where the cost of being
wrong is higher than the cost of the inference.

**Reach for it when:** the work is genuinely "decide what to do", not "do
the thing". When you'd want a second pair of eyes on a difficult problem.

## Decision questions

Answer in order. Stop at the first "yes":

1. **Is the deliverable a string of tokens with a clear template?**
   (Commit message, doc comment, test stub from a signature, boilerplate
   DTO, obvious wiring.)
   → **Tier 1: Local Qwen.**

2. **Is the design settled, and the work is implementing it?**
   (Translating an agreed proposal to code, writing tests for a clear
   spec, refactoring a single file, fixing a localised bug.)
   → **Tier 2: Copilot Sonnet.**

3. **Does the work require choosing between non-obvious alternatives,
   reasoning across multiple files, or debugging something that resists
   the first three explanations you can think of?**
   → **Tier 3: BYOK Opus.**

4. **If you can't decide between tiers,** start one tier lower than your
   first instinct and escalate if the lower tier struggles. Most people
   default too high.

5. **If genuinely stuck, ask Chris.** Don't escalate silently.

## Cost-conscious patterns

- **Plan with Opus, execute with Sonnet, scaffold with Qwen.** The same
  pattern works at every scale. Opus produces the proposal; Sonnet
  implements it cycle by cycle; Qwen fills in the obvious bits.
- **Cache where you can.** Anthropic prompt caching is 90% off the input
  rate for cached reads. If you're hitting the same spec files repeatedly,
  cache them.
- **Don't use Opus for refactor steps.** A refactor that preserves
  behaviour is mechanical by definition. Tier 1 or Tier 2.
- **Don't use Qwen for the type checker.** Two-pass type checking is the
  hardest sustained reasoning in Sprint 1. It's Tier 3 work, full stop.

## How to answer

When this prompt is invoked with a specific task, produce:

```
# Model selection: <task in one phrase>

**Recommendation:** Tier <N> — <model name>.

**Reasoning:** <one or two sentences explaining which decision question
triggered the recommendation, and what that means for the work.>

**Watch for:** <one specific signal that would tell you to escalate. for
tier 1: "if the first attempt produces nonsense, escalate to tier 2." for
tier 2: "if the proposal requires reasoning across more than two files,
escalate to tier 3.">

**Estimated cost:** <approximate, in pence or pounds. for tier 1, "free".>
```
