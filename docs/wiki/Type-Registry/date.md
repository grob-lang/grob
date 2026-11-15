# date — Type Registry

Instance methods and properties on the `date` type. For construction, parsing and
module-level functions see [date module](../Standard-Library/date.md).

## Component Properties

| Member | Signature | Notes |
|--------|-----------|-------|
| `year` | `→ int` | |
| `month` | `→ int` | 1–12 |
| `day` | `→ int` | 1–31 |
| `hour` | `→ int` | 0–23 |
| `minute` | `→ int` | 0–59 |
| `second` | `→ int` | 0–59 |
| `dayOfWeek` | `→ string` | `"Monday"` etc |
| `dayOfYear` | `→ int` | 1–366 |
| `utcOffset` | `→ int` | Minutes |

## Formatting Methods

| Member | Signature | Notes |
|--------|-----------|-------|
| `toIso()` | `→ string` | `"2026-04-05"` |
| `toIsoDateTime()` | `→ string` | `"2026-04-05T14:30:00Z"` |
| `format(pattern: string)` | `→ string` | Custom pattern |

## Arithmetic Methods

| Member | Signature |
|--------|-----------|
| `addDays(n: int)` | `→ date` |
| `addMonths(n: int)` | `→ date` |
| `addHours(n: int)` | `→ date` |
| `addMinutes(n: int)` | `→ date` |

## Comparison Methods

| Member | Signature |
|--------|-----------|
| `isBefore(other: date)` | `→ bool` |
| `isAfter(other: date)` | `→ bool` |

Operators `<`, `>`, `==`, `!=`, `<=`, `>=` also work.

## Interval Methods

| Member | Signature | Notes |
|--------|-----------|-------|
| `daysUntil(other: date)` | `→ int` | Positive if `other` is later |
| `daysSince(other: date)` | `→ int` | Positive if receiver is later |

## Epoch Methods

| Member | Signature |
|--------|-----------|
| `toUnixSeconds()` | `→ int` |
| `toUnixMillis()` | `→ int` |

## Timezone Methods

| Member | Signature |
|--------|-----------|
| `toUtc()` | `→ date` |
| `toLocal()` | `→ date` |
| `toZone(zone: string)` | `→ date` |

## Examples

```grob
now := date.now()
print(now.format("dd MMM yyyy HH:mm"))

created := date.of(2026, 1, 15)
age := created.daysUntil(date.today())
print("${age} days old")

next_month := date.today().addMonths(1)
print("Next month: ${next_month.toIso()}")
```
