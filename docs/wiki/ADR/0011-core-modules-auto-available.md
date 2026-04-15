# ADR-0011: Core Modules Auto-Available

**Status:** Accepted
**Date:** April 2026
**Decision:** Thirteen core modules are auto-available in every script. No `import` required.

## Context

Should `fs.readText()` require `import fs` first? A script with no imports should be self-contained. A script with imports has external dependencies.

## Decision

Core modules (`fs`, `strings`, `json`, `csv`, `env`, `process`, `date`, `math`, `log`, `regex`, `path`, `format`, `guid`) are always in scope. `import` is reserved for plugins — external dependencies that require `grob install`.

## Consequences

Positive: zero-ceremony for common operations. `import` lines are a dependency manifest.

Negative: namespace pollution. Acceptable — thirteen names is manageable, and collision with user functions is resolved by the compiler.
