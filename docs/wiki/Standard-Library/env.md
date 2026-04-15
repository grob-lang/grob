# env — Environment Variables

Environment variable access. Core module — auto-available, no import required.

## Module Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `env.get(key: string)` | `→ string?` | Get value or nil if absent |
| `env.require(key: string)` | `→ string` | Get value or throw `RuntimeError` |
| `env.set(key: string, value: string)` | `→ void` | Set for current process only |
| `env.has(key: string)` | `→ bool` | True if key is present and non-empty |
| `env.all()` | `→ map<string, string>` | All environment variables |

## Examples

```grob
pat := env.require("ADO_PAT")
days := env.get("STALE_DAYS")?.toInt() ?? 30

if (env.has("CI")) {
    log.info("Running in CI mode")
}
```

`env.require()` is the canonical pattern for credentials. `env.set()` is
process-scoped only — does not persist.

See also: [Script Parameters](../Language-Specification/Script-Parameters.md)
