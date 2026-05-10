---
mode: 'agent'
description: 'Read a Sprint definition from grob-v1-requirements.md and produce a TDD spec plus trunk-based-development workflow ready to execute.'
---

# Sprint plan

Your job is to read a specific Sprint from `docs/design/grob-v1-requirements.md`
and produce two artefacts:

1. A **TDD spec** — the ordered list of tests to write, grouped by red/green
   cycle, with explicit named exceptions for structural work.
2. A **trunk-based workflow** — the sequence of short-lived branches that
   deliver the sprint, with proposed commit-message subjects for each.

## Inputs

Ask Chris which sprint to plan if not specified. Then fetch:

- `docs/design/grob-v1-requirements.md` — the sprint definition, Definition of
  Done and acceptance criteria.
- `docs/design/grob-decisions-log.md` — any decisions cited in the sprint.
- Any spec files referenced from the sprint description.

Do **not** load more than you need. Fetch on demand.

## Output structure

```
# Sprint <N> plan

## Scope
<one paragraph restating what the sprint must produce, in your own words.
chris will tell you if you've misread the brief>

## Day-one constraints reaffirmed
<list of the Sprint 1 acceptance gates that apply: error-recovering parser,
SourceLocation on every node, Declaration back-reference, two-pass type
checker, /// doc comment attachment. omit any that don't apply to this
sprint.>

## TDD cycles

### Cycle 1: <short name>
- **Branch:** `feat/<scope>-<kebab-name>`
- **Test to write:** <fully qualified name, e.g.
  Grob.Compiler.Tests.LexerTests.Scan_EmptyInput_ReturnsEofTokenOnly>
- **Test scenario:** <one-sentence description of what's being verified>
- **Expected failure:** <what the red-state error message should be>
- **Minimum implementation:** <what code makes it pass>
- **Refactor candidates:** <what might want cleaning up afterwards>
- **Commit:**
  - `test(<scope>): <subject>`
  - `feat(<scope>): <subject>`
  - `refactor(<scope>): <subject>` (only if needed)

### Cycle 2: ...
(repeat)

## Structural exceptions
<list of tasks that bypass TDD and why — solution skeleton, csproj edits,
directory scaffolding. for each, name the exception explicitly and say what
will be tested after the structure exists.>

## Acceptance check
<from the sprint's Definition of Done, the list of things that must be true
for the sprint to be considered complete. each item should map to either
specific tests above or specific manual checks chris will run.>

## Out of scope
<things you considered putting in this plan but deliberately excluded
because they belong to a later sprint. brief, just enough to confirm
you weren't going to overreach.>
```

## Rules

- **Cycles are small.** A single TDD cycle should be one test, one
  implementation, one refactor at most. If a cycle has three tests and
  spans multiple types, split it.
- **Branches are short-lived.** A branch should land in main within hours,
  not days. If a cycle is so big that it needs its own branch alive for
  more than a day, split the cycle.
- **No `main` work.** Every cycle is on a branch. Plan the branch names
  up front so Chris can spot anything that should be combined or split.
- **Propose, don't execute.** This prompt produces a plan, not code.
  Once Chris agrees, the work proceeds cycle by cycle with proposal
  before code.
- **Model selection per cycle.** For each cycle, note whether the work is
  Sonnet-shaped (routine TDD) or Opus-shaped (genuine design). If unsure,
  ask Chris which tier to use rather than guess.

## What this prompt does not do

- Does not start writing code. The deliverable is the plan.
- Does not modify any files. Read-only operation.
- Does not commit anything. There may not even be a branch yet.

When the plan is ready, end with: "Plan complete. Ready to start Cycle 1?
I'll propose the test first."
