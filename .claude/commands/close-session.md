---
description: Close an increment session — reconcile the deliverables against the prompt, commit, push, open or update the PR and hand off. Use at the end of every session that changed the tree.
argument-hint: [increment prompt path — defaults to the one this session executed]
---

# Close a session

End the session the same way every time. The misses this command exists to stop
are all end-of-session ones: a coverage run that was listed and never made, a
push with no PR, a PR with no description, a gold master changed against the
gate report.

The prompt to reconcile against is **$ARGUMENTS**, or the increment prompt this
session executed.

## 1. Reconcile the deliverables

Re-read the increment prompt's scope, tests and deliverables sections for the
session or change groups this session covered. Write the list out as a checklist
and mark each item **done** or **not done**, with evidence:

| Deliverable | Evidence |
|---|---|
| Tests | the `dotnet test Grob.slnx` result observed this session: passed, failed, skipped |
| Coverage, where listed | the figure and the projects it covers |
| Mutation verifications, where listed | each prediction and what was observed |
| Commits | the SHAs, from `git log --oneline main..HEAD` |
| Documents and register rows | the paths changed |

**Never invent green state.** A figure not observed this session is "not run
this session". Finish anything not done unless the prompt forbids it or a STOP
condition applies; what stays not done is listed as skipped, with the reason.

## 2. Check the constraints

Read the prompt's STOP conditions and "out of scope" list, and the gate report
if the session has one. Confirm none was crossed:

- `git diff --stat main..HEAD` shows no path the prompt puts out of scope.
- No gold master, quarantine row or baseline changed unless the prompt or the
  gate report authorises that change by name.
- No error code, opcode, token or AST node was added outside the increment's
  declared budget.

A crossed constraint is not tidied away here. Stop, say which one and leave the
fix to Chris.

## 3. Commit

Stage the session's files and commit through `/commit-message`. One commit per
change group where the prompt asks for that.

## 4. Push

`git push`, or `git push -u origin <branch>` on the first push. Never to `main`.
The pre-push gate runs; **never bypass it**. If it blocks the push, the session
is not closed: report the failure and stop.

If this command was invoked from a command or agent that stops before the push,
stop here and say so.

## 5. Open the PR, or update it

Count the changed files first: `git diff --name-only main...HEAD`. **Over 100,
stop** and propose a split into stacked PRs before opening anything —
CodeRabbit will not review a larger PR.

Run the pre-PR CodeRabbit pass that `CLAUDE.md`'s quality gates call for if the
PR is not open yet.

Write the description to `.grob-session/pr-body.md` with the Write tool:

```markdown
## Summary
<What this lands and which decision authorised it. Register rows closed.>

## Change groups
1. **<name>** (`<sha>`, `<type(scope)>`): <what changed>

## Verification
<Test counts, coverage and mutation results, each as observed this session.>

## Not run
<Anything listed and not done, with the reason. "Nothing" if nothing.>

## Follow-ups
<Findings for a later increment or for Chris. "None" if none.>
```

- **No PR yet:** `gh pr create --title "<conventional-commits title>"
  --body-file .grob-session/pr-body.md`.
- **PR exists:** update the description so it covers the new commits. `gh pr
  edit --body` fails on this repo, so use a REST `PATCH` on
  `repos/{owner}/{repo}/pulls/<N>` with the body file.

Merging is Chris's action.

## 6. Hand off

- **The increment continues in another session:** run `/handoff`.
- **The branch is ready:** follow `trunk-flow`'s merge protocol, which removes
  the session hand-off before the branch is handed over.

## 7. Report

In chat, the checklist from step 1 with its evidence, then the branch, the head
SHA and the PR URL. Name every skipped item again on its own line so it is not
lost in the table.

Prose follows `house-style`.
