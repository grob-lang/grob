# date — Date and Time

Single date/time type with full arithmetic, timezone support, Unix epoch
conversion and parse/format. Core module — auto-available, no import required.

There is no separate `datetime` type. One type, two constructors. Two types is a
common source of conversion friction.

## Construction

| Function | Signature | Description |
|----------|-----------|-------------|
| `date.now()` | `→ date` | Current date and time |
| `date.today()` | `→ date` | Current date, time zeroed |
| `date.of(year, month, day)` | `→ date` | Date from components |
| `date.ofTime(year, month, day, hour, minute, second)` | `→ date` | Date and time from components |
| `date.fromUnixSeconds(n: int)` | `→ date` | From Unix timestamp (seconds) |
| `date.fromUnixMillis(n: int)` | `→ date` | From Unix timestamp (milliseconds) |

## Parsing

| Function | Signature | Description |
|----------|-----------|-------------|
| `date.parse(str: string)` | `→ date` | Parse ISO 8601 |
| `date.parse(str: string, pattern: string)` | `→ date` | Parse with explicit pattern |

```grob
d := date.parse("2026-04-05")
d2 := date.parse("05/04/2026", "dd/MM/yyyy")
```

## Formatting

| Method | Signature | Description |
|--------|-----------|-------------|
| `toIso()` | `→ string` | `"2026-04-05"` |
| `toIsoDateTime()` | `→ string` | `"2026-04-05T14:30:00+01:00"`, with the value's own offset; `Z` only for a UTC value (`.toUtc()`) |
| `format(pattern: string)` | `→ string` | Custom format |

```grob fragment
d.format("dd MMM yyyy")        // "05 Apr 2026"
d.format("dd/MM/yyyy HH:mm")   // "05/04/2026 14:30"
```

## Arithmetic

| Method | Signature |
|--------|-----------|
| `addDays(n: int)` | `→ date` |
| `addMonths(n: int)` | `→ date` |
| `addHours(n: int)` | `→ date` |
| `addMinutes(n: int)` | `→ date` |

## Comparison

Operators `<`, `>`, `==`, `!=`, `<=`, `>=` work on dates. Methods `isBefore()`
and `isAfter()` are also available.

## Components

All read as properties (no parentheses):

| Property | Type | Description |
|----------|------|-------------|
| `year` | `int` | |
| `month` | `int` | 1–12 |
| `day` | `int` | 1–31 |
| `hour` | `int` | 0–23 |
| `minute` | `int` | 0–59 |
| `second` | `int` | 0–59 |
| `dayOfWeek` | `string` | `"Monday"` etc |
| `dayOfYear` | `int` | 1–366 |
| `utcOffset` | `int` | Minutes |

## Interval Computation

| Method | Signature | Description |
|--------|-----------|-------------|
| `daysUntil(other: date)` | `→ int` | Positive if `other` is later |
| `daysSince(other: date)` | `→ int` | Positive if receiver is later |

Neither throws on direction reversal — negative values are valid.

## Epoch

| Method | Signature |
|--------|-----------|
| `toUnixSeconds()` | `→ int` |
| `toUnixMillis()` | `→ int` |

## Timezone

| Method | Signature | Description |
|--------|-----------|-------------|
| `toUtc()` | `→ date` | Convert to UTC |
| `toLocal()` | `→ date` | Convert to local time |
| `toZone(zone: string)` | `→ date` | Convert to named zone |

Zone names are preferred: `"Europe/London"`, `"America/New_York"`.

## Examples

### File age check

```grob
cutoff := date.today().addDays(-30)

for file in fs.list(`C:\Logs`) {
    if (file.modified < cutoff) {
        file.delete()
        print("Deleted ${file.name}")
    }
}
```

### Days between dates

```grob
created := date.parse("2026-01-15")
age     := created.daysUntil(date.today())
print("Repository is ${age} days old")
```
