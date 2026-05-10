---
name: grob-spec-lookup
description: How to find authoritative answers to design questions in the Grob spec corpus. Use this skill when you need to verify a design decision, look up a constraint, check whether something is settled or find which spec document answers a specific question. The decisions log is the authority; this skill teaches the lookup pattern.
---

# Grob spec lookup

The design corpus lives in `docs/design/`. The decisions log is the authority.
This skill is the map.

## The authority hierarchy

When two sources disagree, the higher-ranked source wins:

1. **`grob-decisions-log.md`** — the authority. Numbered ADR-style entries
   (`D-001` upwards). Every meaningful design decision is recorded here with
   rationale, supersedes/superseded-by links and a body pointing at the
   spec doc for detail.
2. **Wiki ADRs** (`docs/wiki/ADR/`) — architecture-level decisions in full
   ADR format. As of May 2026, sixteen are published: `ADR-0001` through
   `ADR-0014`, `ADR-0016`, `ADR-0017`. ADR-0016 is superseded by D-288 and
   D-291.
3. **Live spec documents** — the rest of the corpus. These reflect what the
   decisions log has settled, but if a spec doc says one thing and the
   decisions log says another, the log wins. Surface the drift rather than
   silently following one source.
4. **`copilot-instructions.md` and skill files** — operational guidance.
   Cannot override the decisions log; if you find a contradiction, the log
   wins and the instruction file needs updating.

## The lookup pattern

When you have a question about Grob's design, follow these steps:

1. **Identify the question type.** Most questions fall into one of these
   shapes:
   - Behaviour question: "What should the parser do when X?"
   - Constraint question: "Can `Grob.Compiler` reference Y?"
   - Type question: "What methods does `string` have?"
   - Format question: "How is the `.grobc` file laid out?"
   - Scope question: "Is feature Z in Sprint 1?"

2. **Pick the spec doc** using the table below.

3. **Fetch and read** the relevant section. Don't load the whole file
   unless you need it.

4. **Cross-check the decisions log** if the answer involves a design
   choice. The spec doc may have lagged behind a recent decision.

5. **Cite what you find.** When using the answer, name the source: "Per
   D-300…", "From `grob-language-fundamentals.md` §29…".

## The map

| Question shape | Spec doc |
| --- | --- |
| Is decision X settled? What's the rationale? | `grob-decisions-log.md` |
| What does the parser do? Type checker? Compiler? | `grob-language-fundamentals.md` |
| Parser error recovery rules | `grob-language-fundamentals.md` §29 |
| What methods does built-in type T have? | `grob-type-registry.md` |
| What's the VM architecture? Opcodes? Stack model? | `grob-vm-architecture.md` |
| What's the `.grobc` binary format? | `grob-grobc-format.md` |
| What stdlib modules exist? | `grob-stdlib-reference.md` |
| What does error code Exxxx mean? | `grob-error-codes.md` |
| What's the sprint scope? Definition of Done? | `grob-v1-requirements.md` |
| Which projects exist? What can reference what? | `grob-solution-architecture.md` |
| What are the validation suite scripts? | `grob-sample-scripts.md` |
| What does the formatter do? | `grob-formatter-specification.md` |
| What open questions remain? | `grob-open-questions.md` |
| What plugins are first-party? | `grob-plugins.md` |
| How does install work? | `grob-install-strategy.md` |
| What's the VS Code / LSP plan? | `grob-tooling-strategy.md` |

When the question doesn't fit the table, default to:
1. The decisions log index — search for the topic.
2. The language fundamentals doc — most "what does Grob do?" questions live
   here.

## Worked examples

### Example 1: a parser behaviour question

**Question:** "What should the parser do when it encounters an unterminated
block comment?"

**Lookup:**
1. Type: behaviour question, parser-specific.
2. Doc: `grob-language-fundamentals.md`, parser section, error recovery
   subsection (§29).
3. Cross-check decisions log: D-300 covers error-recovering parser
   behaviour generally.
4. Citation: "Per D-300 and `grob-language-fundamentals.md` §29, the parser
   synchronises at statement-boundary newlines outside any open bracket, or
   the closing `}` of the enclosing block. An unterminated block comment
   produces an `ErrorStmt` with code E-### and the recovery synchronises
   at the next top-level declaration keyword."

### Example 2: a DAG question

**Question:** "Can `Grob.Stdlib` reference `Grob.Compiler`?"

**Lookup:**
1. Type: constraint question, project layout.
2. Doc: `grob-solution-architecture.md`.
3. Answer: No. `Grob.Stdlib` depends on `Grob.Core` and `Grob.Runtime`
   only. The DAG forbids upward references.
4. Citation: "Per `grob-solution-architecture.md` and the project-layout
   rules in `.github/instructions/project-layout.instructions.md`,
   `Grob.Stdlib` depends only on `Grob.Core` and `Grob.Runtime`.
   Referencing `Grob.Compiler` would violate the DAG."

### Example 3: a question the spec doesn't answer

**Question:** "What happens if a script imports two modules that export
the same name?"

**Lookup:**
1. Type: behaviour question, import resolution.
2. Docs: `grob-language-fundamentals.md` (imports section),
   `grob-v1-requirements.md` (scope).
3. If neither doc has an answer, check `grob-open-questions.md`.
4. If still unanswered, **surface the gap.** Don't invent a resolution.

   "I couldn't find a settled answer on duplicate-name handling across
   imports. `grob-language-fundamentals.md` covers import syntax but
   not collision behaviour. This may need a new decision. Should I
   propose one, or is there an existing source I missed?"

## What you do not do

- **Do not paraphrase the decisions log.** Quote it or point to it.
  Paraphrasing introduces drift.
- **Do not resolve drift in your head.** If two sources disagree, the log
  wins, but surface the inconsistency so the lagging source can be fixed.
- **Do not invent decisions.** If the spec is silent on something, the gap
  is itself a finding. Propose a decision; don't backfill one.
- **Do not load the whole corpus** to answer a single question. Fetch what
  you need.
- **Do not cite from memory.** Always re-fetch when precision matters. The
  spec evolves; old assumptions go stale.

## When the spec is wrong

Sometimes you'll find an actual error in the spec — a contradiction between
two sections of the same doc, a stale reference to a superseded decision,
a typo that changes meaning. When this happens:

1. **Don't fix it silently.** The corpus has authority because it's
   trustworthy; silent fixes erode that.
2. **Surface the finding.** "In `grob-vm-architecture.md` §4.3, the opcode
   list shows OP_LOAD at byte 0x12, but D-298 specifies 0x14. The decisions
   log is authoritative; the spec doc has drifted. Should I propose a fix
   to the spec?"
3. **Wait for Chris's call.** Spec edits are deliberate, not casual.
