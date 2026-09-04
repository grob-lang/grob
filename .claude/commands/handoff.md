---
description: Write a session hand-off to .grob-session/handoff.md so the next session resumes without re-deriving state. Use when an increment will not finish in this session, when context is running short or when the session shows degradation signs.
allowed-tools: Bash(git status:*), Bash(git log:*), Bash(git branch:*), Bash(git diff:*), Read, Grep, Glob, Write
---

# Write a session hand-off

Produce `.grob-session/handoff.md` — the document the next session reads first.

The governing constraint: **the next session must be able to rebuild its picture
from the repository, not from your summary.** If this hand-off is being written
because the session degraded, the narrative is the part that degraded. So the
document is mostly pointers plus the small set of facts that exist only in this
session and vanish when it ends. Those facts are the reason the command exists.

Facts that exist only here, in priority order:

1. Decisions taken this session and not yet written to the decisions log.
2. What was tried and rejected, and why — so the next session does not retry it.
3. The STOP condition that ended the session.

Everything else the next session can read off disk. Do not paraphrase file
contents into the hand-off; cite the path.

## Procedure

1. `git branch --show-current`, `git status --short`, `git log --oneline main..HEAD`.
2. Read the increment prompt currently being executed. Record its **path only**.
3. Establish green state. Report the last `dotnet test` result and coverage figure
   you actually observed this session. If you did not run them, say "not run this
   session" — do not infer, and do not run a full suite purely to fill the field.
4. Check whether any error code allocated this session reached `ErrorCatalog.cs`
   (the D-316 gate). An allocated-but-unregistered code is the most expensive thing
   to lose across a session boundary.
5. Write the file. Create `.grob-session/` if absent.
6. In chat, report **the path and the next action only**. Not the contents — the
   file exists so the document is read in the editor, matching the convention that
   investigation reports go to a scratch file with a pointer in chat.

## The document

```markdown
# Session hand-off — <date>

## Where we are
- Branch: <name>, HEAD <sha>
- Working tree: <clean | N files modified — list them>
- Commits on this branch: <N>  (`git log --oneline main..HEAD`)
- Increment prompt: <path>
- Phase reached: <e.g. "Phase 1 investigation complete, no edits made">

## Green state
- Tests: <result, or "not run this session">
- Coverage: <figure and projects, or "not run this session">
- Pre-push gate: <run / not run>

## Decisions taken, not yet logged
<The highest-value section. Each one: what was decided, the reasoning, and which
D-### it should become. If none, write "none" — do not pad.>

## Error codes and registry state
<Codes allocated this session. For each: registered in ErrorCatalog.cs, or not.
D-316 green or not. If nothing was allocated, write "none".>

## Tried and rejected
<Approaches attempted and abandoned, each with the reason. This is what stops the
next session burning a third of its budget rediscovering a dead end.>

## Quarantined or skipped tests
<Each with its documented reason. If none, "none".>

## Why this session stopped
<The STOP condition. Scope exceeded, a finding that needs the maintainer, context
exhaustion, degradation, or the increment simply not finishing.>

## Next session, first action
<One imperative sentence. Not a plan — the first thing to do.>

## Open for the maintainer
<Anything needing Chris's call before work continues. If nothing, "nothing".>
```

## Rules

- **Never invent green state.** "Not run this session" is a useful fact. A wrong
  coverage figure carried into the next session is worse than an absent one.
- **Never fold an unlogged decision into prose and call it recorded.** The
  hand-off is a holding place, not the decisions log. The next session still owes
  `logging-a-decision` its three-location lockstep entry.
- **Do not commit `.grob-session/`.** It is session scratch and gitignored.
- **Do not summarise the increment prompt.** Path only. A paraphrase written by a
  degraded session is a corrupted copy of a file that is sitting right there.
- Prose follows `house-style`.

## When to reach for this without being asked

Offer a hand-off when two or more of these hold:

- A file already read this session is being re-read.
- A constraint already established is being restated as if new.
- A question already answered is being asked again.
- Work is drifting outside the increment's stated scope.
- The plan-mode or read-only gate was crossed without an explicit approval turn.

Any one of those alone is ordinary. Two together mean the session's grip on its
own context has slipped, and continuing costs more than stopping. Say so plainly
and offer the hand-off — do not push on and do not wait to be asked.
