# CsvTable

Returned by `csv.read()`, `csv.parse()`, `csv.stdin()`.

| Member | Kind | Signature | Notes |
|--------|------|-----------|-------|
| `headers` | property | `→ string[]` | Empty if `hasHeaders: false` |
| `rowCount` | property | `→ int` | |
| `rows` | property | `→ CsvRow[]` | |
| `mapAs<T>()` | method | `→ T[]` | Typed deserialisation |

See also: [CsvRow](CsvRow.md), [csv module](../Standard-Library/csv.md)
