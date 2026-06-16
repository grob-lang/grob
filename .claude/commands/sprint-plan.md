---
description: Read a Sprint definition from grob-v1-requirements.md and produce a TDD spec plus trunk-based-development workflow ready to execute.
argument-hint: [sprint-number]
---

# Sprint plan

Your job is to read a specific Sprint from `docs/design/grob-v1-requirements.md` and
produce two artefacts:

1. A **TDD spec** — the ordered list of tests to write, grouped by red/green cycle,
   with explicit named exceptions for structural work.
2. A **trunk-based workflow** — the sequence of short-lived branches that deliver the
   sprint, with proposed commit-message subjects for each.

The sprint to plan is **$ARGUMENTS** (ask if not given).

## Inputs

Fetch:

- `docs/design/grob-v1-requirements.md` — the sprint definition, Definition of Done and
  acceptance criteria.
- `docs/design/grob-decisions-log.md` — any decisions cited in the sprint.
- Any spec files referenced from the sprint description.

Do **not** load more than you need. Fetch on demand.

## Output structure

```
# Sprint <N> plan

## Scope
<one paragraph restating what the sprint must produce, in your own words.>

## Day-one constraints reaffirmed
<list of the acceptance gates that apply: error-recovering parser, SourceLocation on
every node, Declaration back-reference, two-pass type checker. omit any that don't
apply to this sprint.>

## TDD cycles

### Cycle 1: <short name>
- **Branch:** `feat/<scope>-<kebab-name>`
- **Test to write:** <fully qualified name>
- **Test scenario:** <one sentence>
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
<tasks that bypass TDD and why — solution skeleton, csproj edits, scaffolding. name
the exception and say what will be tested after the structure exists.>

## Acceptance check
<from the Definition of Done, the list of things that must be true for the sprint to be
complete. each maps to specific tests above or specific manual checks.>

## Out of scope
<things deliberately excluded because they belong to a later sprint.>
```

## Rules

- **Cycles are small.** One test, one implementation, one refactor at most. If a cycle
  has three tests across multiple types, split it.
- **Branches are short-lived** — land within hours, not days.
- **No `main` work.** Every cycle is on a branch. Plan the branch names up front so
  anything that should be combined or split is visible.
- **Propose, don't execute.** This command produces a plan, not code. Read-only;
  modifies no files. Once agreed, the work proceeds cycle by cycle with proposal before
  code.
- **Model per cycle.** For each cycle, note whether the work is Sonnet-shaped (routine
  TDD) or Opus-shaped (genuine design). If unsure, ask which to use rather than guess.

When the plan is ready, end with: "Plan complete. Ready to start Cycle 1? I'll propose
the test first."
