# Interlude Increment B — Hardening Report

> Supply-chain and build-integrity hardening of the Grob repository, run at the
> Sprint 4 → 5 interlude after Increment A (D-316) landed and its drift gate was
> green. Additive only: no `src/` language code, no spec language content, no
> error catalog and no test logic were changed. This complements §11 of
> `grob-v1-requirements.md` (language/runtime security) and does not overlap it.
>
> Authorising decision: **D-317**. Deferral tracked as **OQ-018**.

---

## Starting reality — the repository was already substantially hardened

The increment prompt is framed as greenfield supply-chain work. On disk it is
not. Before this increment the repository already had: Central Package
Management, a committed `packages.lock.json` per project, a single pinned NuGet
source, SHA-pinned GitHub Actions with least-privilege `permissions`, Dependabot
for NuGet and Actions, Trivy, TruffleHog, a .NET 10.0.8 runtime floor enforced in
`Directory.Build.props`, and CodeQL via GitHub Advanced Security default setup.

This increment is therefore **gap-fill plus a few honest corrections**, not a
build from zero. Each control below states what already existed and what B added.

---

## Findings surfaced (stop-and-surface, not silently actioned)

- **F-B1 — the prompt's dependency list is wrong, and `CLAUDE.md` drifts.** The
  prompt names `SonarAnalyzer.CSharp` and `FsCheck` as build dependencies.
  Neither is in the build. The real direct set, read from
  `Directory.Packages.props` and the lockfiles, is six packages:
  `BenchmarkDotNet` 0.15.8, `OmniSharp.Extensions.LanguageServer` 0.19.9,
  `coverlet.collector` 10.0.1, `Microsoft.NET.Test.Sdk` 18.6.0, `xunit` 2.9.3,
  `xunit.runner.visualstudio` 2.8.2. `CLAUDE.md` states "SonarCloud is enforced
  via `SonarAnalyzer.CSharp` … in `Directory.Build.props`"; that is inaccurate —
  Sonar runs via the `dotnet-sonarscanner` workflow, not a NuGet analyzer
  package. **Action:** did not add the named packages; recorded the corrected
  set. No `CLAUDE.md` edit (out of B's additive scope); raised for a later
  decision.
- **F-B2 — CodeQL has no workflow file but is wired.** CodeQL runs via GitHub
  Advanced Security **default setup**, confirmed in the repository's
  Advanced-Security settings (CodeQL analysis: Default setup, last scan minutes
  before this work). Default setup scans the default branch and pull requests —
  exactly what §2.2 requires. Adding an advanced `codeql.yml` would error against
  default setup. **Action:** documented default setup; added no workflow file.
- **F-B3 — TruffleHog cannot meet the §6 negative proof.** It runs
  `--only-verified`, failing only on a live, verifiable credential, so a planted
  fake secret does not trip it. **Action:** added a gitleaks CI gate
  (pattern-based, working tree and full history) alongside TruffleHog.
- **F-B4 — floating versions contradicted "every version pinned."** Three
  dependencies floated (`0.15.*`, `0.19.*`, `10.0.*`) via
  `CentralPackageFloatingVersionsEnabled`. **Action:** pinned to the exact
  versions the lockfiles already resolved (no resolution change) and removed the
  floating flag; Dependabot now drives bumps as reviewable PRs.
- **F-B5 — an unpinned global tool.** `sonarcloud.yml` ran
  `dotnet tool install --global dotnet-sonarscanner` with no version pin.
  **Action:** pinned it (and CycloneDX) in `.config/dotnet-tools.json`; switched
  the workflow to `dotnet tool restore`.

**Numbering confirmed against the live corpus:** D-316 is present (Increment A),
so **D-317** is the first free decision number; the latest open question is
OQ-017, so **OQ-018** is the first free. Both confirmed before writing.

**Delivery shape:** the prompt asks for a single zip. In this harness the
deliverable is the `chore/interlude-b-hardening` branch with the changes in the
working tree and a local commit; pushing and the PR are the maintainer's actions.

---

## The six controls — change and proof

Each control proves it fires before it is trusted. Proofs were run locally except
the gitleaks negative proof, deferred to the first CI run by maintainer decision
(running a freshly-downloaded scanner binary locally was declined).

### 2.1 Dependency integrity

**Already present:** CPM, per-project lockfiles, single pinned source,
`RestorePackagesWithLockFile=true`.

**Added (D-317):**

- `Directory.Packages.props`: every version pinned to an exact value; the three
  floats pinned to 0.15.8 / 0.19.9 / 10.0.1; `CentralPackageFloatingVersionsEnabled`
  removed.
- `nuget.config`: `packageSourceMapping` (`*` → the single `nuget.org` source)
  and `fallbackPackageFolders` cleared (dependency-confusion guard).
- `Directory.Build.props`: `NuGetAudit=true`, `NuGetAuditMode=all`,
  `NuGetAuditLevel=low`. With the pre-existing `TreatWarningsAsErrors`, an
  advisory escalates to a build error.
- The nine lockfiles whose requested range changed (the three pinned packages and
  every test project that pulls `coverlet`) were regenerated with
  `--force-evaluate`; resolved versions and content hashes are unchanged. The
  eleven lockfiles unaffected by the pins were left untouched.

**Negative proof.** Temporarily mismatched a pin (`xunit` 2.9.3 → 2.9.2) and ran
`dotnet restore --locked-mode`:

```text
error NU1004: The package reference xunit version has changed from [2.9.3, ) to
[2.9.2, ). The packages lock file is inconsistent with the project dependencies
so restore can't be run in locked mode.
```

**Positive proof.** Reverted, then `dotnet restore --locked-mode` succeeded with
no error across all twenty projects.

### 2.2 Vulnerability scanning

**Already present:** Trivy filesystem scan; CodeQL default setup; Dependabot
(NuGet + Actions).

**Added (D-317):** `.github/workflows/nuget-audit.yml` — locked-mode restore then
`dotnet list Grob.slnx package --vulnerable --include-transitive --format json`,
with the structured JSON parsed (any `vulnerabilities` entry fails the job). The
JSON form is stable across CLI locale and wording changes, unlike the console
text. CodeQL documented as default setup (F-B2).

**Negative proof.** Planted a known-vulnerable package (`System.Net.Http` 4.3.0).

- Restore-time NuGetAudit gate fired:
  `error NU1903: … 'System.Net.Http' 4.3.0 has a known high severity
  vulnerability, https://github.com/advisories/GHSA-7jgj-8wvc-jh57`.
- The `dotnet list --vulnerable` banner (demonstrated on an isolated project to
  avoid the repo's warnings-as-errors masking the listing) produced
  `Project 'vp' has the following vulnerable packages` with the
  `System.Net.Http … High` row — the exact string the workflow greps to fail.

**Positive proof.** Baseline and post-revert:
`dotnet list package --vulnerable --include-transitive` reports
"no vulnerable packages given the current sources" for all twenty projects.

### 2.3 Deterministic and reproducible builds

**Added (D-317):** `Directory.Build.props` — `Deterministic=true`,
`ContinuousIntegrationBuild=true` under CI only (`$(GITHUB_ACTIONS) == 'true'`),
`EmbedUntrackedSources=true`, `PublishRepositoryUrl=true`. SourceLink is built
into the .NET 10 SDK for GitHub, so no package was added. `global.json`
(10.0.300, `rollForward: latestFeature`, no prerelease) is consistent with D-310
and unchanged. Analyzer/tool versions are pinned via CPM and
`.config/dotnet-tools.json`.

**Positive proof.** Built `Grob.Core` twice with the deterministic flags into
separate output directories:

```text
ec4daadd99d198eabfdee439510af93e6a528b20310b2f868b8edb89e03a881d  det1/Grob.Core.dll
ec4daadd99d198eabfdee439510af93e6a528b20310b2f868b8edb89e03a881d  det2/Grob.Core.dll
```

Byte-identical.

### 2.4 Workflow hardening

**Already present:** every action SHA-pinned with a version comment;
least-privilege `permissions`; no `pull_request_target`; `persist-credentials:
false` on the benchmark checkout.

**Added (D-317):** `persist-credentials: false` on every other `actions/checkout`
(ci, sonarcloud, trivy, trufflehog, release); `concurrency` cancel-in-progress on
ci, sonarcloud, trivy, trufflehog and the two new workflows (not release —
releases are never cancelled); `step-security/harden-runner` in audit mode on the
Linux jobs (skipped on the Windows matrix leg, which it does not support). New
workflows default `contents: read`.

**Negative proof.** A SHA-pin policy check (every `uses:` ref must be a 40-hex
commit SHA) flags a planted `actions/checkout@v4`:

```text
UNPINNED:       - uses: actions/checkout@v4
POLICY FIRES: unpinned action caught (job would fail)
```

**Positive proof.** The same check passes across all eight real workflows: every
action is SHA-pinned. All eight workflows also parse as valid YAML.

### 2.5 Secret hygiene

**Already present:** TruffleHog (verified credentials, full history on push, PR
diff on pull requests); GitHub push protection enabled.

**Added (D-317):** `.github/workflows/gitleaks.yml` — pinned gitleaks v8.30.1 CLI,
`gitleaks dir .` (working tree) and `gitleaks git .` (full history), both
`--redact --exit-code 1`. The subcommands and flags were confirmed against the
v8.30.1 documentation. `.gitignore` extended for local `sbom.json` and
`gitleaks-report.json`. `.grob/` already covers `.grob/packages` and tool caches.

**`@secure` confirmation.** Audited every workflow step: none echo a script
parameter or secret value. `nuget-audit` echoes only the package report;
`gitleaks` runs with `--redact`. The language-level `@secure` discipline is not
undermined by any CI log step.

**Negative proof — deferred to first CI run (maintainer decision).** gitleaks is
not installed locally and running a freshly-downloaded binary was declined. The
gitleaks gate (plant a fake secret → job fails; remove → green) will be exercised
on the first CI run. The workflow command syntax is documentation-verified for
v8.30.1.

**Positive / history scan.** The repository currently carries no committed
secret: this is corroborated by the existing TruffleHog full-history scan, which
is green on `main`. The new gitleaks history scan provides a second, pattern-based
confirmation from the first CI run.

### 2.6 Provenance scaffolding

**Added (D-317):**

- `.config/dotnet-tools.json` pinning CycloneDX 6.2.0 and dotnet-sonarscanner
  11.2.1 (F-B5).
- An `sbom` job in `ci.yml`: `dotnet tool restore` then
  `dotnet CycloneDX Grob.slnx --output . --filename sbom.json --output-format
  Json`, uploading `sbom.json` as a build artifact on every CI run.
- `actions/attest-build-provenance` (SHA-pinned) in `release.yml`, on the
  `create-release` job with `id-token: write` + `attestations: write`. Guarded by
  the workflow trigger (`push: tags: ['v*']`): it attests the published zip/tar
  exactly when a release ships and is inert otherwise — real, not a no-op false
  claim.

**Positive proof.** Generated the SBOM locally: a valid CycloneDX 1.7 JSON
document, 74,991 bytes, 62 components, written to `sbom.json`. The attestation
step is present in `release.yml` and correctly guarded behind the tag trigger.

---

## Verification summary

| Control | Negative proof | Positive proof |
| --- | --- | --- |
| Dependency integrity | pin mismatch → `NU1004` locked-mode failure | clean `--locked-mode` restore, 20 projects |
| Vulnerability scanning | planted CVE → `NU1903` + `dotnet list` banner | zero vulnerable packages baseline |
| Deterministic builds | — | byte-identical `Grob.Core.dll` across two builds |
| Workflow hardening | unpinned action caught by policy | all 8 workflows SHA-pinned and valid YAML |
| Secret hygiene | gitleaks gate — deferred to first CI run | repo clean (TruffleHog history green) |
| Provenance | — | SBOM produced (CycloneDX 1.7, 62 components); attestation present and tag-guarded |

**Regression gate.** The full build is clean (0 warnings, 0 errors) and the whole
test suite passes (every test project green). The Increment A drift gate
(`tests/Grob.Consistency.Tests` / `Grob.DriftCheck`) is green before and after
this increment — D-317's decisions-log entry keeps the index↔entry lockstep
exact.

---

## Explicitly deferred → OQ-018

- **Code signing of `grob.exe`.** No shipping executable in v1; signing belongs
  with the first public release.
- **Full SLSA Level 3 / signed release artifacts.** Scaffolded now (SBOM, build
  provenance), completed at first release.
- **Reproducible-build attestation across machines**, beyond the deterministic
  compilation already in place.

These are tracked in OQ-018, which also corrects the record: signing is unrelated
to OQ-013 (the `Grob.Llm` plugin); the earlier association was a framing error.

---

## Post-review hardening (SonarCloud + CodeRabbit on PR #76)

The first CI run surfaced review findings, all addressed on the same branch:

- **SonarCloud `githubactions:S6506` (gitleaks.yml)** — `curl -L` could follow a
  redirect down to plaintext HTTP. Fixed by forcing `--proto '=https'
  --proto-redir '=https' --tlsv1.2`, and by adding a pinned-SHA256 integrity
  check of the gitleaks tarball before install. This cleared the new-code
  security rating from C back to A.
- **CodeRabbit — harden-runner parity** — the two new workflows (`gitleaks.yml`,
  `nuget-audit.yml`) were missing the `step-security/harden-runner` audit step
  present on every other Linux job. Added to both.
- **CodeRabbit — gitleaks download integrity** — addressed by the SHA256
  verification above.
- **CodeRabbit — brittle text grep** — the NuGet-audit gate now parses
  `--format json` instead of grepping the English console banner.

All other checks on PR #76 were green on the first run, including the new
gitleaks "Secret scan", "NuGet vulnerability gate" and "SBOM (CycloneDX)" jobs,
CodeQL, Trivy and TruffleHog.

---

## Files changed

- **Edited:** `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`,
  `.gitignore`, `SECURITY.md`,
  `.github/workflows/{ci,sonarcloud,trivy,trufflehog,release,benchmark}.yml`,
  nine `packages.lock.json` files, `docs/design/grob-decisions-log.md` (D-317,
  three-location lockstep — the Project Status milestone is unchanged, so the
  status table is not applicable), `docs/design/grob-open-questions.md` (OQ-018).
- **Added:** `.config/dotnet-tools.json`, `.github/workflows/nuget-audit.yml`,
  `.github/workflows/gitleaks.yml`, this report.
- **Untouched:** all `src/` language code, every spec document's language
  content, the error catalog and registry, all test logic, `global.json`.
