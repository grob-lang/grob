# Interlude Increment B — Hardening

> Self-contained increment prompt. Sprint 4 → 5 interlude. Adds no language
> surface. Run against a fresh repo zip in its own session, **after Increment A
> has landed and its drift gate is green**. Deliver full files only, in a single
> zip. Propose before code — plan mode is the approval gate.

-----

## 0. Objective

Harden the Grob project itself — its dependencies, its build, its CI workflows,
its secret hygiene, and its provenance. This is supply-chain and build-integrity
work on the repository, distinct from §11 of `grob-v1-requirements.md`, which
covers language and runtime safety (`process.run`, `@secure`, plugin-as-arbitrary
-code). The two do not overlap.

Scope is what is real for v1. The runtime is not yet a shipping artifact —
"compile to executable" is post-MVP and winget distribution is future — so
**code signing of `grob.exe` is out of scope for this increment** and is tracked
as a deferred open question (OQ-018, §7). Build the supply-chain floor now;
attach signing to the first public release.

-----

## 1. Closed surface — do not touch

Additive only. This increment does **not** modify:

- Any `src/` language code.
- Any spec document's language content.
- The error catalog or registry.
- Test logic (it may add CI steps that *run* tests; it does not change them).

It **may** add and edit, repo-root and CI scope only:

- `.github/workflows/*.yml`, `.github/dependabot.yml`.
- `Directory.Build.props`, `Directory.Packages.props`, `global.json`,
  `nuget.config`.
- `packages.lock.json` per project.
- `.config/` tool manifests.
- `SECURITY.md`.
- The decisions log, for the D-317 entry and the OQ-018 entry.

If Increment A's drift gate fails at any point during B, stop — B has touched
something it should not have, or surfaced a real inconsistency. Resolve before
continuing.

-----

## 2. Scope — six controls

Each control ships with negative and positive proof (§6). Where a control
depends on the live dependency set, read it from the restored solution — do not
assume the dependency list; Grob's own build pulls SonarAnalyzer.CSharp,
BenchmarkDotNet, the OmniSharp LSP packages, xUnit, FsCheck and coverlet, among
others, and the exact set must come from the repo, not this prompt.

### 2.1 Dependency integrity

- Central Package Management: `Directory.Packages.props` with
  `ManagePackageVersionsCentrally`. Every version pinned in one place.
- `nuget.config` with a single pinned source and `packageSourceMapping`, fallback
  folders disabled.
- `packages.lock.json` committed per project. CI restores with locked mode
  (`--locked-mode`) so an unexpected transitive change fails the restore rather
  than resolving silently.
- `NuGetAudit` enabled with `NuGetAuditMode=all` and a chosen `NuGetAuditLevel`.

### 2.2 Vulnerability scanning

- CI gate: `dotnet list package --vulnerable --include-transitive` parsed to fail
  the job on any reported advisory.
- Confirm CodeQL is wired (CI-only is acceptable) and runs on the default branch
  and on pull requests.
- `.github/dependabot.yml` for NuGet and GitHub Actions ecosystems, so dependency
  and action bumps arrive as reviewable PRs.

### 2.3 Deterministic and reproducible builds

- `Directory.Build.props`: `Deterministic=true`,
  `ContinuousIntegrationBuild=true` under CI, `EmbedUntrackedSources=true`,
  SourceLink for the repo host.
- `global.json` SDK pin confirmed and consistent with D-310's corrected pinning,
  with an explicit `rollForward` policy.
- Analyzer and tool versions pinned through Central Package Management so the
  same inputs produce the same outputs.

### 2.4 Workflow hardening

- Pin every GitHub Action to a full commit SHA, not a moving tag.
- Least-privilege `permissions:` — default `contents: read`, with the minimal
  explicit write scope only on the jobs that need it.
- `persist-credentials: false` on checkout.
- Optional egress control (`step-security/harden-runner`) in audit mode first.
- `concurrency` to cancel superseded runs. No `pull_request_target` patterns that
  expose secrets to fork PRs.

### 2.5 Secret hygiene

- A secret-scanning step in CI (gitleaks or equivalent) failing the job on a hit.
- A history scan to confirm no secret has been committed previously; report the
  result.
- `.gitignore` coverage confirmed for local artefacts that could carry
  sensitive paths — `.grob/packages`, local baselines, tool caches.
- Confirm the language-level `@secure` discipline is not undermined by any CI log
  step that would echo a parameter value.

### 2.6 Provenance scaffolding

- SBOM generation (CycloneDX) producing an `sbom.json` build artifact.
- Build-provenance attestation scaffolding (`actions/attest-build-provenance`),
  wired but guarded so it activates when artifacts are actually published — not a
  no-op left undocumented, but not a false claim of provenance on a build that
  ships nothing yet.
- A short `SECURITY.md` documenting the supported version, the vulnerability
  disclosure route, and the provenance posture.

-----

## 3. Explicitly deferred

State these in the deliverable and in OQ-018 so they are tracked, not forgotten:

- **Code signing of `grob.exe`.** No shipping executable in v1. Signing belongs
  with the first public release. Do not invent a signing decision; the prior
  association of signing with OQ-013 was mistaken — OQ-013 on disk is the
  `Grob.Llm` plugin.
- **Full SLSA Level 3 / signed release artifacts.** Scaffolding now, completion
  at first release.
- **Reproducible-build attestation across machines** beyond deterministic
  compilation — release-time concern.

-----

## 4. Decision to log — D-317

Create the authorising entry. Confirm **D-317** is still the first free number
against the live log before writing.

Four-location lockstep: summary index row, full entry, status table if
applicable, footer changelog.

**Entry content:**

- **Title:** Project hardening interlude — supply-chain, build, workflow, secret
  and provenance integrity.
- **Area:** Tooling — supply chain.
- **Body:** Additive hardening of the Grob repository: central package
  management with committed lockfiles and locked-mode restore, NuGet and CodeQL
  vulnerability gates, deterministic builds with SourceLink, SHA-pinned
  least-privilege workflows, CI secret scanning, and SBOM and build-provenance
  scaffolding. Complements §11 language/runtime security; does not overlap it.
  Code signing deferred to first release (OQ-018).

-----

## 5. Open question to log — OQ-018

Allocate **OQ-018** (confirm it is the first free OQ number against the live
open-questions document before writing).

- **Title:** Code signing and release provenance for the first public artifact.
- **Body:** When the runtime first ships as a distributable artifact, decide the
  signing approach and complete the provenance chain the B increment scaffolds.
  This is a deliberate deferral, not an omission. It is unrelated to OQ-013
  (`Grob.Llm` plugin); the earlier association was a framing error and is
  corrected here.
- **Defer until:** first public release of a `grob` executable or installer.

-----

## 6. Verification — negative and positive proof per control

Each control proves it fires before it passes:

- **Dependency integrity** — break the lockfile or add an unlisted transitive and
  confirm locked-mode restore fails; restore the clean state and confirm it
  passes.
- **Vulnerability scanning** — plant a known-vulnerable package version and
  confirm the gate fails; remove it and confirm green.
- **Deterministic builds** — build twice and confirm byte-identical output for
  the deterministic surface.
- **Workflow hardening** — confirm an unpinned action or an over-broad
  `permissions` block is caught by the chosen lint/policy; confirm the hardened
  workflows pass.
- **Secret hygiene** — plant a fake secret and confirm the scan fails the job;
  remove it and confirm green.
- **Provenance** — confirm the SBOM is produced and attached as an artifact, and
  the attestation step is present and correctly guarded.

A control with no demonstrated failure mode is not trusted.

-----

## 7. Deliverable

A single zip containing:

- All added and edited CI, build and config files at their repo paths
  (`.github/`, `Directory.Build.props`, `Directory.Packages.props`,
  `global.json`, `nuget.config`, per-project `packages.lock.json`, `.config/`,
  `SECURITY.md`).
- The full updated `grob-decisions-log.md` with D-317.
- The full updated `grob-open-questions.md` with OQ-018.
- A hardening report as `interlude-B-report.md` recording each control, its
  negative and positive proof, and the deferred items.

Full files, never patches. One concern per branch; main is protected.
