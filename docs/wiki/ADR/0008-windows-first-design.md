# ADR-0008: Windows-First Design

**Status:** Accepted
**Date:** April 2026
**Decision:** Grob is designed, documented and tested Windows-first. All examples use Windows paths and commands.

## Context

The primary developer works on Windows 11. The target user base is primarily Windows developers and sysadmins. Azure CLI, PowerShell replacement and Windows automation are the driving use cases.

## Decision

All documentation uses Windows-idiomatic patterns. `C:\Reports` not `./reports`. No `cat`, `ls` or other Unix commands in examples. `winget` as the primary distribution mechanism.

## Consequences

Positive: documentation matches the target audience's environment. No cognitive translation required.

Negative: POSIX users must mentally translate paths. Acceptable — the runtime works cross-platform; only the docs are Windows-first.
