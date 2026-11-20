# ADR-0007: Windows-First Design

**Status:** Superseded by D-442 — see "What Changed Since This ADR" below
**Date:** April 2026
**Decision:** Grob is designed Windows-first. All paths, examples and tooling default to Windows conventions.

## Context

Most scripting tools are designed for Unix and adapted to Windows as an
afterthought. PowerShell is the notable exception but its syntax is hostile.
Grob's primary developer and target user base are on Windows.

## Decision

Windows-native by default:

- All documentation examples use Windows paths (`C:\\Reports`, not `./reports`)
- No `cat`, `ls` or other Unix-isms in documentation
- `winget install Grob.Grob` as the primary install method
- Windows Terminal profile ships with Grob
- `path.separator` returns `\` on Windows
- Install paths use `%USERPROFILE%` and `%ProgramFiles%`

Cross-platform is not excluded — .NET runs on Linux and macOS. But Windows is
the design target, not a second-class citizen.

## Alternatives Considered

**Unix-first** — the default for most language tooling. Would alienate the
primary user base and miss the opportunity to serve Windows developers who are
underserved by scripting tools.

**Platform-neutral** — use only relative paths and avoid platform-specific
conventions in docs. Produces documentation that helps nobody concretely.

## Consequences

Positive: Windows developers see themselves in the documentation immediately.
Install experience is native and frictionless.

Negative: Linux and macOS users see Windows paths in examples. This is
documented as intentional. Path-handling functions (`path.join()`,
`path.normalise()`) are platform-aware at runtime.

## What Changed Since This ADR

This ADR made Windows the design target and treated other platforms as possible
but unpromised. **D-442** (November 2026) replaces that: Windows and Linux are
equal platforms, with the same language, standard library and diagnostics, and
every release ships both. D-442, not this ADR, describes current platform
support.

Two things here are reversed. Documentation examples no longer default to
Windows paths: an example uses a relative path valid on both platforms, or shows
both forms where an absolute path is the subject. And platform-neutral
documentation, rejected above, is now the rule, because a person or an agent on
either platform must be able to run an example as written.

What stands: Windows is never an afterthought. Backtick raw strings remain the
idiom for Windows paths, and the Windows Terminal profile and `winget` delivery
stay as Windows-only features. macOS is not claimed.
