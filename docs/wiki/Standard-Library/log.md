# log — Structured Logging

Structured logging for unattended scripts. Core module — auto-available, no
import required. All output goes to stderr.

## Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `log.debug(message: string)` | `→ void` | Suppressed by default; visible under `--verbose` |
| `log.info(message: string)` | `→ void` | Informational |
| `log.warning(message: string)` | `→ void` | Warning |
| `log.error(message: string)` | `→ void` | Error (logs only — does not throw) |
| `log.setLevel(level: string)` | `→ void` | Set runtime threshold |

Output format: `[LEVEL]  message` — no timestamp by default.

`print()` is stdout for script results. `log.*` is stderr for operational
messages. These never mix.

`log.setLevel()` accepts `"debug"`, `"info"`, `"warning"`, `"error"`. Suppresses
all levels below the threshold.
