---
description: Triage and respond to PR review comments (CodeRabbit, SonarCloud, human reviewers) — fix or push back with reasons, commit locally, reply on the PR. Works on the first pass or as a follow-up.
argument-hint: [PR number — defaults to the current branch's PR]
---

# Review and respond to PR comments

Work through the review feedback on PR **$ARGUMENTS** (if no number is given,
resolve the PR for the current branch with `gh pr view --json number`). The job is
to triage every comment, **fix what is right and push back on what is wrong**,
commit the fixes locally, and reply on the PR so each thread is closed out.

This is a judgement task — use Opus. The standing rules still hold: TDD is
non-negotiable, the `ErrorCatalog`/§3.1.1/DAG invariants gate acceptance, and
**push is the maintainer's** — this command stops after the local commit.

## 1. Gather every feedback source

Derive the repo once: `REPO=$(gh repo view --json nameWithOwner -q .nameWithOwner)`.

- **Reviews + their bodies:** `gh pr view <N> --json reviews,comments,title,headRefName`.
  - **CodeRabbit** (`coderabbitai`) posts a review whose body holds the *actionable
    summary* and a `🧹 Nitpick comments` block, plus a walkthrough as an issue
    comment. Read the nitpicks — they are inside that body, not in the actionable
    list.
  - **SonarCloud** (`sonarqubecloud`) posts an issue comment with the quality gate
    and a **New issues** count. The gate can pass while new issues remain — the
    bar is **zero new issues**, not a passing gate. **Pull the specifics directly
    with the `sonar-pr-review` skill** (it hits the SonarCloud Web API over `curl`
    — no Docker MCP, no opening the web UI), passing it this PR number. It returns
    each new-code issue as `severity · rule · path:line · message`, the gate
    verdict with its failing conditions, and the new-code line/branch coverage
    against the 90% floor (D-328). Feed each new issue into the step 2 triage; the
    **New issues** count is the number to drive to zero. The skill **only fetches**
    — fix-versus-push-back, including the hot-path suppression call below, stays in
    step 2. If the skill is unavailable, the same finding (for example cognitive
    complexity over 15) is often mirrored as a CodeRabbit nitpick, which is usually
    enough to act on; the SonarCloud comment's link is the last-resort fallback.
  - **Human reviewers** — treat their comments as higher priority than the bots.
- **Inline review threads (with reply ids):**
  `gh api repos/$REPO/pulls/<N>/comments --jq '.[] | "\(.id)\t\(.path):\(.line // .original_line)\t\(.user.login)"'`,
  then read each body. These are the threads you reply into in step 4.

**Follow-up pass.** If you have run this before on the PR, only act on what is
**new or still open**: threads with no reply from us, threads where a bot replied
again after our last response, and any review posted after our last commit. Do not
re-open or re-litigate a thread already resolved — read our prior replies and the
commit history first. Re-run `sonar-pr-review` on a follow-up too: the **New
issues** set shrinks as fixes land, and a clean fetch (gate green, zero new issues)
is the Sonar half of done.

## 2. Triage each finding — fix or push back

For every comment decide one of: **fix**, **push back**, or **skip as stale**.

- **Verify against the current code first.** Bots flag things that are already
  fixed, or that never applied. If a finding is no longer valid, skip it and say so
  in the reply with a one-line reason — do not change code to satisfy a stale note.
- **Fix when the finding is right.** Most CodeRabbit "potential issue" and all
  SonarCloud new issues should be fixed. Cognitive complexity over the threshold in
  **compile-time code** (type checker, compiler) → extract helpers, no behaviour
  change. A test that asserts only an error code → assert the full diagnostic
  contract (code **and** line **and** column, equality, not inequalities).
- **Weigh performance before accepting a structural fix — it decides push-back.**
  On a **hot path** — the VM dispatch loop, the lexer — an "extract a method" or
  cognitive-complexity flag is usually a **push-back**, not a fix: the inline,
  branch-free dispatch is deliberate (D-302), and extracting per-opcode handlers
  adds a call frame per instruction. SonarCloud already suppresses the relevant
  rules (S3776 cognitive complexity, S1244 float equality) for those files in
  `.github/workflows/sonarcloud.yml`; if a new hot-path complexity issue appears,
  the fix is usually to extend that suppression with a one-line rationale, not to
  refactor the loop. The same rule extracted into a helper is fine in compile-time
  code, where it is off the execution hot path. State the perf trade-off explicitly
  in the reply so the push-back is on the record.
- **Before deferring as "spec-silent" or out-of-scope, check for an existing
  rule/code that already covers it.** A local check masquerading as a flow analysis
  (5A bare-return → E0005) should be fixed now, not deferred.
- **Push back when the bot is wrong.** Reply in the thread with the reasoning and
  the authority (a `D-###`, a `§`, the closed-enum/DAG invariant, or a hot-path
  performance characteristic per the bullet above) — and do **not** change the
  code. Verify the citation before leaning on it.
- **Surface, don't silently decide, a genuine design fork.** If a comment exposes a
  real spec/decision question, raise it with Chris rather than inventing a
  resolution in either direction.

## 3. Apply the fixes (TDD, green, formatted)

Follow the `tdd-cycle` skill — for each code fix write or adjust the test first,
confirm it fails, then make it pass. Never skip the red step. Then:

- `dotnet build Grob.slnx` and `dotnet test Grob.slnx` — zero warnings
  (`TreatWarningsAsErrors`), zero failures.
- `dotnet format whitespace Grob.slnx` before staging (the pre-commit hooks mutate
  but do not restage — running it first avoids an aborted cycle).
- If a fix touches coverage, keep affected projects at or above the bar and note
  any change. Re-run `sonar-pr-review` only after the next push has re-scanned —
  the API reflects the **last completed analysis**, so locally-committed fixes show
  up on the maintainer's push, not before.

## 4. Commit locally — do not push

Stage only the review-fix files and commit via the `commit-message` skill
(conventional commits, `Co-Authored-By` trailer, body explaining *why*). Reference
the PR in the body (for example `Addresses PR #<N> review (CodeRabbit + SonarCloud).`).

**Stop after the local commit.** Per the `trunk-flow` skill, pushing is the
maintainer's action. Capture the commit SHA for the report.

## 5. Respond on the PR

Replies will post **before** the maintainer pushes, so phrase "fixed" replies as
*committed locally, lands on push* — never dangle a SHA that is not on GitHub yet.

- **Inline threads** — reply into each with
  `gh api repos/$REPO/pulls/<N>/comments -f body="..." -F in_reply_to=ID`:
  - fixed: "Fixed — committed locally as `SHA` (lands on the next push)", plus one line on the change.
  - pushed back: "Pushing back:" with the reason and its authority.
  - stale: "Already handled / no longer applies:" with the reason.
- **Summary** — one top-level `gh pr comment <N> --body "..."` listing every item
  and its disposition, plus the final build/test result. Include the Sonar line
  from `sonar-pr-review`: gate verdict and **New issues** count (the target is
  zero), so the comment records the Sonar state at the time of the pass.
- **gh quirks:** `gh pr edit --body` fails on this repo (projectCards) — if you
  must change the PR body, use a REST `PATCH`. The token can expire mid-session; if
  a `gh` call 401s, that is why. A `401` from a **SonarCloud** call instead is the
  `SONAR_TOKEN` (a different token from the `gh` one) — re-export it, do not confuse
  the two.

## 6. Report back to Chris

Give a short triage table — **fixed / pushed back / skipped-stale**, one line each —
the local commit SHA (unpushed), the Sonar state from `sonar-pr-review` (gate +
New issues count), and confirmation that the thread replies and the summary comment
are posted. Remind him: the push is his, and the bots re-run on push, so a fresh
CodeRabbit/Sonar pass will follow.

**Model:** Opus — this is triage and judgement, not transcription.
