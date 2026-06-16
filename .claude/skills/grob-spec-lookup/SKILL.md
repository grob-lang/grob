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
3. **Live spec documents** — the rest of the corpus under `docs/design/`.
   These reflect what the decisions log has settled, but if a spec doc says
   one thing and the decisions log says another, the log wins. Surface the
   drift rather than silently following one source.
4. **Operational guidance** — the Claude Code harness:
   the root `CLAUDE.md`, the nested `CLAUDE.md` files (`src/` for C# host
   code, `tests/`, `plugins/`), the sub-agents under `.claude/agents/`
   (`grob-compiler-engineer`, `grob-reviewer`, `grob-design-reviewer`), the
   slash commands under `.claude/commands/`, and the skills under
   `.claude/skills/`. Cannot override the decisions log; if you find a
   contradiction, the log wins and the operational file needs updating.

## The lookup pattern

When you have a question about Grob's design, follow these steps:

1. **Identify the question type.** Most questions fall into one of these
   shapes:
    - Behaviour question: "What should the parser do when X?"
    - Constraint question: "Can `Grob.Compiler` reference Y?"
    - Type question: "What methods does `string` have?"
    - Format question: "How is the `.grobc` file laid out?"
    - Scope question: "Is feature Z in Sprint 1?"
    - Rationale question: "Why was approach A chosen over B?"

2. **Pick the spec doc** using the table below.

3. **Fetch and read** the relevant section. Don't load the whole file
   unless you need it.

4. **Cross-check the decisions log** if the answer involves a design
   choice. The spec doc may have lagged behind a recent decision.

5. **Cite what you find** following the conventions below.

## The map

Technical and design questions only — public-facing material and
historical sketches are listed at the end of this section under
"Out-of-scope sources".

| Question shape                                                                                                        | Spec doc                                                     |
| --------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| Is decision X settled? What's the rationale?                                                                          | `grob-decisions-log.md`                                      |
| Has X been decided yet? (quick check)                                                                                 | `grob-decisions-log.md` — start at the summary index         |
| Why was approach A chosen over approach B at the architectural level?                                                 | `docs/wiki/ADR/`                                             |
| What does the parser do? Type checker? Compiler?                                                                      | `grob-language-fundamentals.md`                              |
| Parser error recovery rules                                                                                           | `grob-language-fundamentals.md` §29                          |
| Day-one constraints (`SourceLocation`, `Declaration` back-references, two-pass type checker, error-recovering parser) | `grob-language-fundamentals.md` + the implementer agent file |
| What methods does built-in type T have?                                                                               | `grob-type-registry.md`                                      |
| What's the VM architecture? Opcodes? Stack model?                                                                     | `grob-vm-architecture.md`                                    |
| What's the `.grobc` binary format?                                                                                    | `grob-grobc-format.md`                                       |
| What stdlib modules exist?                                                                                            | `grob-stdlib-reference.md`                                   |
| What does error code Exxxx mean?                                                                                      | `grob-error-codes.md`                                        |
| What's the error-code numbering scheme?                                                                               | ADR-0014                                                     |
| Is error code Exxxx stable / can I change its message?                                                                | ADR-0017                                                     |
| What's the sprint scope? Definition of Done?                                                                          | `grob-v1-requirements.md`                                    |
| Which projects exist? What can reference what?                                                                        | `grob-solution-architecture.md`                              |
| What are the validation suite scripts?                                                                                | `grob-sample-scripts.md`                                     |
| What does the formatter do?                                                                                           | `grob-formatter-specification.md`                            |
| What open questions remain?                                                                                           | `grob-open-questions.md`                                     |
| What plugins are first-party?                                                                                         | `grob-plugins.md`                                            |
| How does install work?                                                                                                | `grob-install-strategy.md`                                   |
| What's the VS Code / LSP plan?                                                                                        | `grob-tooling-strategy.md`                                   |
| What's the tone of error messages? REPL prompt? CLI conventions? Sparky's presentation?                               | `grob-personality-identity.md`                               |

When the question doesn't fit the table, default to:

1. The decisions log summary index — scan for the topic.
2. The language fundamentals doc — most "what does Grob do?" questions
   live here.
3. `project_knowledge_search` with distinctive keywords from the
   question. Use it before reaching for arbitrary file paths.

### Out-of-scope sources

These exist in the corpus but are not authoritative and should not be
cited as design sources:

- `grob-language-brainstorm.md` — early sketch notes. Historical context
  only.
- `grob-early-design-conversation.md` — historical sketch transcript.
- `sharpbasic-retrospective.md` — Grob design _inputs_, not Grob spec.
- `grob-linkedin-series-bible.md` — public-facing series reference. Not a
  technical source.

If you find yourself wanting to cite one of these for a design question,
stop. The answer either lives in an authoritative source you haven't
found yet, or the question is genuinely undecided and needs a new
decision.

## Finding the right doc when you don't know which

Two discovery mechanisms, in order of preference:

1. **`project_knowledge_search`.** The fastest path to the right
   document when you know the topic but not the file. Search with
   distinctive content words from the question — type names, error
   codes, opcodes, module names, decision numbers. Avoid generic words
   like "design", "rules", "structure" that match too broadly.

2. **The decisions log summary index.** The top of
   `grob-decisions-log.md` is a numbered index of every D-### entry
   with a one-line summary. Scanning it is the right move for "has X
   been decided?" questions before reading any spec doc — if the index
   lists a relevant entry, jump straight to it; if it doesn't, the
   answer is likely either in a spec doc (use search) or genuinely
   undecided.

Don't load full files when you don't know which one. Search first.

## Citation conventions

When you cite a source in a response, use one of these forms — never
both for the same fact:

- **For settled design decisions:** `Per D-300, …` or `D-300 specifies …`.
  The decisions log is the authority; citing the entry number is
  sufficient.
- **For architecture-level rationale:** `Per ADR-0014, …`. ADR number, no
  file path needed.
- **For spec detail beyond what a decision summarises:**
  `From grob-language-fundamentals.md §29, …` or
  `grob-type-registry.md lists …`. File path with section if applicable.
- **For operational rules:** `Per src/CLAUDE.md,
…` or `Per the implementer agent, …`. Full path, or the agent's name.

When a fact has both a D-### and a spec section, the D-### is the
authoritative citation; the spec section is supplementary. Don't double up.

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
   produces an `ErrorStmt` with the appropriate Exxxx-series code, and
   recovery synchronises at the next top-level declaration keyword."

### Example 2: a DAG question

**Question:** "Can `Grob.Stdlib` reference `Grob.Compiler`?"

**Lookup:**

1. Type: constraint question, project layout.
2. Doc: `grob-solution-architecture.md`.
3. Answer: No. `Grob.Stdlib` depends on `Grob.Core` and `Grob.Runtime`
   only. The DAG forbids upward references.
4. Citation: "Per `grob-solution-architecture.md` and the project-layout
   rules in `src/CLAUDE.md`,
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

### Example 4: a rationale question

**Question:** "Why is the bytecode VM stack-based rather than register-based?"

**Lookup:**

1. Type: rationale question — _why_, not _what_.
2. The decisions log will have a D-### entry; the wiki ADR will have the
   full architectural reasoning.
3. `project_knowledge_search` with "stack-based VM register" gets there
   fast.
4. Citation: "Per D-297 and ADR-0013, the VM is stack-based because I/O
   latency is the bottleneck for scripting workloads, not instruction
   dispatch; register-based complexity has no meaningful payoff for this
   workload."

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
- **Do not cite supplementary sources as authority.** The brainstorm and
  early-design files are background, not source of truth.

## When an authoritative document is wrong

Sometimes you'll find an actual error in an authoritative document — a
contradiction between two sections, a stale reference to a superseded
decision, a typo that changes meaning. This applies to any document with
authority: spec docs, decisions log entries, ADRs, instruction files,
agent files, skills.

When this happens:

1. **Don't fix it silently.** These documents have authority because
   they're trustworthy; silent fixes erode that.
2. **Surface the finding.** "In `grob-vm-architecture.md` §4.3, the opcode
   list shows OP_LOAD at byte 0x12, but D-298 specifies 0x14. The
   decisions log is authoritative; the spec doc has drifted. Should I
   propose a fix?"

    Or: "The reviewer agent checks `[ExcludeFromCodeCoverage]`
    justifications, but `src/CLAUDE.md` doesn't actually state
    what counts as substantive. The instruction file should be the
    source; the reviewer is enforcing a rule the instruction file
    doesn't quite spell out. Should I propose tightening the instruction
    file?"

3. **Wait for Chris's call.** Edits to authoritative documents are
   deliberate, not casual. The fix path may be a decisions log entry, a
   spec edit, an instruction-file update, or all three — Chris decides
   which.
