---
mode: 'agent'
description: 'Produce a structured proposal for a multi-file change, ready for Chris to approve or push back on before any code is written.'
---

# Propose a change

Use this when the work in front of you is non-trivial: spans more than
one file, introduces an abstraction, affects public API or touches the
project layout. Do not write the production code yet. Produce the proposal,
get agreement, then execute.

For obvious mechanical work — a single-method body, an obvious wiring,
a one-line fix — skip the proposal and ask: "this looks mechanical, should
I implement it directly?" If Chris says yes, do it. If no, come back here.

## Proposal structure

```
# Proposal: <one-line summary>

## What
<one paragraph: what the change does at the level a code reviewer would
care about. not the implementation — the outcome.>

## Why
<one paragraph: why now, what triggered it, what spec entry or decision
authorises it. cite the D-### or the spec section.>

## Files touched

| File | Change | Lines (approx) |
| ---- | ------ | -------------- |
| <path> | <new file / modified / deleted> | <approx scope> |

## Public API impact
<either "none" or a list of: types added, types changed, types removed.
if anything is removed or signature-changed, that's a breaking change
and you should explicitly flag it.>

## Tests
<for each public behaviour introduced or changed, name the test that
will cover it. for each behaviour preserved, name the existing test
that proves it. if there isn't one, that gap is itself a finding —
say so.>

## Design alternatives considered
<at least one alternative you ruled out. one sentence on why the
proposed approach beats it. if you can't name an alternative, you
probably haven't thought about the problem hard enough yet.>

## Risks and unknowns
<things you're not sure about. things that might break. anything you
want chris to weigh in on before you start writing code.>

## Branch and commits

- **Branch:** `<type>/<scope>-<kebab-name>`
- **Commit sequence:**
  1. `test(<scope>): <subject>`
  2. `feat(<scope>): <subject>`
  3. ... (continue as needed)

## Decision needed
<one specific question, or "agree to proceed". no fishing — be concrete
about what chris is being asked to approve.>
```

## Rules

- **Propose, never assume.** If something isn't covered by an existing
  decision or spec entry, the proposal must call that out explicitly.
  Don't quietly resolve it in the implementation.
- **Cite the spec.** Every meaningful design choice in the proposal should
  cite a decision-log entry (`D-###`), an ADR or a spec section. If you
  can't cite, the choice is new and Chris needs to weigh in.
- **One concern per proposal.** If the proposal touches two unrelated
  concerns, split it into two proposals. The "files touched" table is a
  good test — if you can draw a clean line between the rows, that's two
  proposals.
- **The "design alternatives considered" section is mandatory.** It's
  the difference between "here's a thing I want to do" and "here's why
  this is the right thing to do". Don't skip it.
- **Be specific in the decision needed.** "Approve to proceed" is fine
  for a clear proposal. "What do you think?" is not — Chris will push
  back and you'll have lost a turn.

## After agreement

Once Chris agrees:

1. Confirm you're not on `main`. Create the branch if needed.
2. Write the first test from the commit sequence.
3. Run it. Confirm it fails for the expected reason.
4. Propose the minimum implementation that makes it pass.
5. Continue cycle by cycle until the proposal is complete.

If anything you discover during implementation contradicts the proposal,
**stop and surface it** rather than silently revise. The proposal is a
contract.
