# Security Policy

## Supported Versions

Grob is pre-1.0. There are no supported versions yet. Once 1.0 ships, the most recent major version will receive security fixes; older major versions will not.

## Reporting a Vulnerability

Use GitHub Security Advisories to report a vulnerability: navigate to the **Security** tab of this repository and click **"Report a vulnerability"**.

Do not file a public issue for security vulnerabilities.

We aim to acknowledge reports within 7 days. Fix timelines depend on severity.

## Scope

**In scope:** the Grob compiler, runtime, VM and standard library as shipped from this repository.

**Out of scope:** third-party plugins, scripts written in Grob (those are the script author's responsibility) and any deployment of Grob in a specific environment.

## Disclosure

We will coordinate disclosure timing with the reporter. Credit will be given in the security advisory unless the reporter prefers anonymity.

## Provenance and supply chain

The repository is hardened against supply-chain and build-integrity risks (D-317):

- **Dependencies** are centrally managed and pinned to exact versions, with a
  committed `packages.lock.json` per project. CI restores in locked mode, so an
  unexpected transitive change fails the build rather than resolving silently.
  NuGet uses a single pinned source with package-source mapping.
- **Vulnerability scanning** runs on every push and pull request: restore-time
  `NuGetAudit`, a `dotnet list package --vulnerable` CI gate, Trivy, and CodeQL.
  CodeQL runs via GitHub Advanced Security **default setup** (configured in
  repository settings, scanning the default branch and pull requests).
- **Secret scanning** runs gitleaks (pattern-based, full history and working
  tree) and TruffleHog (verified credentials, full history) on every push and
  pull request. GitHub push protection is enabled.
- **Builds are deterministic** with SourceLink, and GitHub Actions workflows are
  pinned to full commit SHAs with least-privilege permissions.
- **A CycloneDX SBOM** (`sbom.json`) is produced as a build artifact on every CI
  run.
- **Release artifacts** are attested with build provenance
  (`actions/attest-build-provenance`) when a `v*` tag is published.

**Not yet in place (deferred to the first public release — OQ-018):** code
signing of the `grob` executable, signed release artifacts and full SLSA Level 3
provenance. The runtime is not yet a shipping artifact, so signing is attached to
the first public release rather than scaffolded against a build that ships
nothing.
