# format — Output Formatting

Output formatting. Core module — auto-available, no import required.

## Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `format.table(items: T[])` | `→ string` | Auto-columns from struct fields |
| `format.table(items: T[], columns: string[])` | `→ string` | Explicit column selection |
| `format.list(item: T)` | `→ string` | Key: value per line |
| `format.csv(items: T[])` | `→ string` | Comma-delimited with header |
| `format.number(value, pattern: string)` | `→ string` | .NET pattern |
| `format.date(value: date, pattern: string)` | `→ string` | .NET pattern |

All functions return `string`. The caller decides what to do with it.

## Examples

```grob
print(results.format.table())
print(results.format.table(columns: ["repo", "staleCount"]))
print(item.format.list())

label := format.number(total, "N2")        // "1,234.56"
label := format.number(pct, "P1")          // "12.3%"
label := format.date(d, "dd MMM yyyy")     // "05 Apr 2026"
```

`.format.table()` and `.format.list()` in chained position are compiler-recognised
— rewritten to free-function form at compile time.
