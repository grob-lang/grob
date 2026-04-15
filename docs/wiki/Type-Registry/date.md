# date

Single type for date and time. Instance methods for arithmetic, formatting,
comparison and timezone conversion.

## Properties

| Member | Type | Notes |
|--------|------|-------|
| `year` | `int` | |
| `month` | `int` | |
| `day` | `int` | |
| `hour` | `int` | |
| `minute` | `int` | |
| `second` | `int` | |
| `dayOfWeek` | `string` | "Monday" etc |
| `dayOfYear` | `int` | |
| `utcOffset` | `int` | Minutes |

## Methods

| Method | Signature | Notes |
|--------|-----------|-------|
| `addDays(n: int)` | `→ date` | |
| `minusDays(n: int)` | `→ date` | |
| `addMonths(n: int)` | `→ date` | |
| `addHours(n: int)` | `→ date` | |
| `addMinutes(n: int)` | `→ date` | |
| `daysUntil(other: date)` | `→ int` | Positive if other is later |
| `daysSince(other: date)` | `→ int` | Positive if receiver is later |
| `isBefore(other: date)` | `→ bool` | |
| `isAfter(other: date)` | `→ bool` | |
| `toIso()` | `→ string` | "2026-04-05" |
| `toIsoDateTime()` | `→ string` | "2026-04-05T14:30:00Z" |
| `format(pattern: string)` | `→ string` | .NET pattern |
| `toUnixSeconds()` | `→ int` | |
| `toUnixMillis()` | `→ int` | |
| `toUtc()` | `→ date` | |
| `toLocal()` | `→ date` | |
| `toZone(zone: string)` | `→ date` | |

Comparison operators `<`, `>`, `==`, `!=`, `<=`, `>=` are supported.

See also: [date module](../Standard-Library/date.md)
