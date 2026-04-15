# date — Date and Time

Date and time operations. Core module — auto-available, no import required.
Single type holds both date and time.

## Module Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `date.now()` | `→ date` | Current date and time (local) |
| `date.today()` | `→ date` | Current date, time zeroed (local) |
| `date.of(year, month, day)` | `→ date` | Construct date (local) |
| `date.ofTime(year, month, day, hour, minute, second)` | `→ date` | Construct date-time (local) |
| `date.parse(str, pattern?: string)` | `→ date` | Parse string (ISO 8601 default) |
| `date.fromUnixSeconds(epoch: int)` | `→ date` | From Unix timestamp (UTC) |
| `date.fromUnixMillis(epoch: int)` | `→ date` | From Unix millis (UTC) |

## Examples

```grob
cutoff := date.today().minusDays(30)
d := date.parse("2026-04-05")
d := date.parse("05/04/2026", "dd/MM/yyyy")

print(d.format("dd MMM yyyy"))    // "05 Apr 2026"
print(d.toIso())                   // "2026-04-05"

if (created.daysUntil(date.today()) > 90) {
    log.warning("Resource is old")
}
```

Constructors default to local time. `date.fromUnixSeconds()` returns UTC.
Use `toUtc()`, `toLocal()`, `toZone()` for timezone conversion.

See also: [date type](../Type-Registry/date.md)
