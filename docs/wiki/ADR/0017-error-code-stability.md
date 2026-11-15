# ADR-0017: Error Code Stability

**Status:** Accepted; clarified by D-410 for a `pre-release` code that never shipped — see "What Changed Since This ADR" below
**Date:** April 2026

**Decision:** Error codes are immutable once shipped. A code number is its
identity and is never reused.

## Context

Error codes appear in documentation, `--explain` output, editor integrations,
and user scripts. If codes change meaning between releases, all of those
downstream consumers break silently.

## Decision

**Error codes are immutable once shipped.** An error code assigned in one
release means the same thing in all subsequent releases.

**Retired codes are never reused.** If an error condition is removed or merged,
its code is marked as retired in the registry but the number is never reassigned.

**Codes are not renumbered.** If the category scheme changes, existing codes
retain their original numbers. New codes are allocated in the correct category.

## Alternatives Considered

**Semantic versioning for error codes** — major version bump allows renumbering.
Breaks all external references that survive the version boundary. Rejected.

**Codes can be reused after a deprecation period** — still breaks references.
Rejected.

## Consequences

Positive: scripts, documentation, editor integrations, and CI pipelines that
reference error codes are stable across upgrades. The `--explain Exxxx`
documentation is permanently valid.

Negative: the registry accumulates retired codes over time. Mitigated by clear
documentation of retired status in the registry.

## What Changed Since This ADR

This ADR's "mark retired, never reuse" mechanism is written for codes that have
shipped. It said nothing about a `pre-release` code removed before any release
carried it. **D-410** (August 2026) answered that case:

- **A `pre-release` code that never shipped may be removed outright** rather than
  marked retired. It leaves no retired row in the registry.
- **Its number is permanently burned.** It is never reused, exactly as for a retired
  shipped code. A gap in the numbering costs nothing (ADR-0014 already expects gaps);
  a reused number would mean one string with two meanings across a public record.

**D-425** (September 2026) was the first to apply it, removing E4202. The decision
above is unchanged for every code that has shipped.
