---
name: sonar-pr-review
description: >
  Use during a manual PR review to pull SonarQube Cloud (SonarCloud) findings for the
  pull request straight from the Web API — issues on new code, the quality-gate verdict
  and the new-code coverage against the floor — without the Docker-based Sonar MCP
  server. Produces a compact findings block for the reviewer to fold into its PR
  comment. It does not post anything; the human composes and posts the comment. Assumes
  the PR's Sonar scan has already completed (the reviewer is triggered manually after
  all scans).
---

# sonar-pr-review

Fetch SonarCloud's PR findings over plain HTTP and hand them to the PR reviewer. This
replaces the Sonar MCP server (which runs in Docker) with three `curl` calls and `jq`.
Nothing here posts to the PR — it produces a findings block the reviewer incorporates
into the comment the human triggers and sends.

## Preconditions

- The PR's SonarCloud analysis has finished (the review is triggered manually after all
  scans, so this holds). If no analysis is found for the PR, **fail clearly** — say so
  and stop; do not present an empty result as a clean bill.
- `curl`, `jq` and `gh` are on `PATH`.
- A SonarCloud user token is in `$SONAR_TOKEN` (the same token the CI scan uses). Never
  echo it, never paste it into a comment, never write it to a file.

## Step 1 — resolve inputs

1. **PR number.** Use the argument if given, else the PR for the current branch:
   ```bash
   PR="${1:-$(gh pr view --json number -q .number)}"
   ```
   If neither yields a number, stop and say the PR could not be resolved.
2. **Org and project key.** Read them from the scan config, do not hardcode:
   ```bash
   PROPS="sonar-project.properties"
   SONAR_ORG="${SONAR_ORG:-$(grep -E '^sonar\.organization=' "$PROPS" | cut -d= -f2- | tr -d '[:space:]')}"
   SONAR_KEY="${SONAR_PROJECT_KEY:-$(grep -E '^sonar\.projectKey=' "$PROPS" | cut -d= -f2- | tr -d '[:space:]')}"
   ```
   If the file or either key is absent, also try `Directory.Build.props` / `.sonarcloud`
   or the env overrides `SONAR_ORG` / `SONAR_PROJECT_KEY`. If still unresolved, **fail
   clearly** naming where you looked — do not guess a key.
3. **Token.** Confirm `$SONAR_TOKEN` is set; if not, stop with a one-line instruction to
   export it. Do not print its value.

## Step 2 — confirm the PR analysis exists (and get the gate)

```bash
GATE=$(curl -fsS -H "Authorization: Bearer $SONAR_TOKEN" \
  "https://sonarcloud.io/api/qualitygates/project_status?projectKey=${SONAR_KEY}&pullRequest=${PR}")
```
If the call fails or `.projectStatus` is absent, the PR has no analysis — **stop and say
so** (most likely the scan has not run for this PR, or the key/PR is wrong). Otherwise
read `.projectStatus.status` (`OK` / `ERROR`) and `.projectStatus.conditions[]`
(`metricKey`, `actualValue`, `comparator`, `errorThreshold`, `status`).

## Step 3 — fetch the issues on the PR's new code

```bash
curl -fsS -H "Authorization: Bearer $SONAR_TOKEN" \
  "https://sonarcloud.io/api/issues/search?componentKeys=${SONAR_KEY}&pullRequest=${PR}&resolved=false&ps=500&p=1"
```
`ps` is the page size (max 500); if `.paging.total` exceeds `ps`, page with `&p=2`, `&p=3`
until covered. For each `.issues[]` keep: `rule`, `severity` (and `.impacts[]` if present
— newer Sonar carries the clean-code severity there), `component` (strip the
`key:` prefix to a repo-relative path), `line`, `message`, `type`. Security hotspots are a
separate endpoint if you want them:
`/api/hotspots/search?projectKey=${SONAR_KEY}&pullRequest=${PR}`.

## Step 4 — fetch the new-code coverage (the floor check)

```bash
curl -fsS -H "Authorization: Bearer $SONAR_TOKEN" \
  "https://sonarcloud.io/api/measures/component?component=${SONAR_KEY}&pullRequest=${PR}&metricKeys=new_coverage,new_line_coverage,new_branch_coverage,new_violations,new_duplicated_lines_density"
```
New-code metric values come back under the measure's period — read `.period.value` and
fall back to `.value`. This is where the 90% line+branch floor (D-328) shows up per-PR.

## Step 5 — format the findings block

Produce one compact block for the reviewer. No preamble, no token, no raw JSON:

```
SonarCloud — PR #<n>  (<project key>)
Quality gate: PASS | FAIL
  <only the failing conditions, each: metric actual vs threshold>
New-code coverage: line <x>% / branch <y>%   (floor 90% — OK | BELOW)
Issues on new code: <count>
  BLOCKER/CRITICAL first, then MAJOR, MINOR, INFO:
  <severity> · <rule> · <path>:<line> · <message>
  ...
```

Sort issues by severity descending. Group obvious clusters (same rule, many sites) as a
count with the first two sites and "(+N more)" rather than listing forty identical hits.
If the gate is green and there are no new-code issues, say exactly that in one line — a
clean result stated plainly, not padded.

## Hand-off

Give the reviewer the block as input, not as the final word — it folds Sonar's findings
in alongside its own read of the diff. Do **not** post a comment from this skill; the
human triggers and sends the PR comment.

When called from `review-pr-comments`, this is the **gather** step (its step 1) and the
block is the Sonar half of the feedback sources. Each new-code issue becomes a triage
item in that command's step 2 — **this skill only fetches**. Fix-versus-push-back is the
reviewer's call: in particular, a hot-path complexity or float-equality flag (VM dispatch
loop, lexer) is usually extended suppression in `.github/workflows/sonarcloud.yml` with a
rationale, not a refactor (D-302), while the same rule in compile-time code is extracted
into a helper. Do not pre-judge any of that here; present the issue and let step 2 decide.
The **New issues** count is the number the review drives to zero — a passing gate with new
issues is not done.

## Notes

- **Naming.** SonarCloud is now branded SonarQube Cloud; the v1 Web API host is still
  `https://sonarcloud.io/api/...` with `Authorization: Bearer <token>`. Some endpoints
  also exist on the v2 host `https://api.sonarcloud.io/`; the three used here are stable
  v1.
- **Rate limits.** Some Cloud APIs are rate-limited. Three calls per review is well
  within them; do not loop these in a tight retry.
- **Fail clearly over fail quiet.** Every unresolved input (token, key, PR, missing
  analysis) stops with a one-line reason. An empty issue list from a real analysis is a
  pass; an empty issue list from a missing analysis is a bug — distinguish them via the
  Step 2 gate check.
- **Self-hosted caveat.** On old self-hosted SonarQube Server the `pullRequest` filter on
  `issues/search` was historically unreliable; on SonarCloud it is the supported path.
  This skill targets SonarCloud.
- **Two tokens, do not confuse them.** A `401` here is the `SONAR_TOKEN`, which is a
  different token from the `gh` token `review-pr-comments` uses for the PR replies.
  Re-export `SONAR_TOKEN`; a `gh` `401` is a separate problem.
