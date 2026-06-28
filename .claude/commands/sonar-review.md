---
description: "Pull SonarCloud findings for a PR (issues on new code, quality-gate verdict, new-code coverage vs the floor) via the Web API, with no Docker MCP. Manually triggered after scans complete. Produces a findings block for the PR review; does not post anything."
allowed-tools: Bash, Read, Grep
argument-hint: "[pr-number]  (defaults to the PR for the current branch)"
model: sonnet
---

# /sonar-review

Fetch SonarCloud's findings for a pull request and print a compact findings block, using
the `sonar-pr-review` skill. Run this manually once all scans on the PR have completed —
typically as the first step of a PR review, so the findings are in hand before reviewing
the diff.

> **Standalone vs in-review.** A full review uses `/review-pr-comments`, which calls the
> `sonar-pr-review` skill in its gather step and triages the findings alongside CodeRabbit
> and human comments. Use `/sonar-review` for a quick ad-hoc Sonar check on its own — the
> gate verdict, the New issues list and the new-code coverage — without the full triage,
> commit and reply pass.

**PR:** `$1` if supplied, otherwise the PR for the current branch (`gh pr view --json
number -q .number`).

Do the following:

1. Read and follow `.claude/skills/sonar-pr-review/SKILL.md` exactly.
2. Resolve the token (`$SONAR_TOKEN`), the org and project key (from
   `sonar-project.properties`), and the PR number. Fail clearly on any missing input —
   name what is missing, do not guess.
3. Confirm the PR has a Sonar analysis (the quality-gate call). If none, stop and say so
   — do not present an empty result as clean.
4. Fetch issues on new code, the quality-gate verdict and the new-code coverage; format
   the findings block per the skill.
5. Print the block only. Do **not** post a PR comment and do **not** echo the token.

This produces input for the review — fold it into the comment you compose and send
manually; it is not the final word.
