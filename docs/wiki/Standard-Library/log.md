# log — Structured Logging

Levelled logging to stderr. Core module — auto-available, no import required.

`print()` is stdout for script results. `log.*` is stderr for operational
messages. These never mix.

## Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `log.debug(message: string)` | `→ void` | Debug level — suppressed by default |
| `log.info(message: string)` | `→ void` | Informational |
| `log.warning(message: string)` | `→ void` | Warning |
| `log.error(message: string)` | `→ void` | Error — logs only, does not throw or halt |
| `log.setLevel(level: string)` | `→ void` | Set runtime threshold |

All output goes to stderr. `log.debug()` is suppressed by default and visible
only under `--verbose`.

Output format: `[LEVEL]  message` — no timestamp by default.

`log.setLevel()` accepts `"debug"`, `"info"`, `"warning"`, `"error"`. Suppresses
all levels below the threshold.

`log.error()` logs only — it does not throw or halt execution. To stop the
script after logging an error, call `exit(1)` explicitly.

## Examples

### Operational logging

```grob
log.info("Starting deployment to ${environment}")
process.runOrFail("az", ["deployment", "create", "--name", name])
log.info("Deployment complete")
```

### Conditional severity

```grob
for drive in drives {
    if (drive.used_pct >= 90) {
        log.warning("${drive.name}: ${drive.used_pct}% used")
    } else {
        log.info("${drive.name}: ${drive.used_pct}% used")
    }
}
```

### Debug output

```grob
log.debug("Processing ${files.length} files")

for file in files {
    log.debug("  ${file.name} — ${file.size} bytes")
    // ...
}
```

Visible only when running with `--verbose`:

```
grob run script.grob --verbose
```

## File output

File output is not in scope for v1. No structured/JSON logging in v1.
