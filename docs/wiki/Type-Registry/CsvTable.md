# CsvTable — Type Registry

Returned by `csv.read()`, `csv.parse()` and `csv.stdin()`.

## Properties

| Member | Signature | Notes |
|--------|-----------|-------|
| `headers` | `→ string[]` | Empty array if `hasHeaders: false` |
| `rowCount` | `→ int` | Number of data rows |
| `rows` | `→ CsvRow[]` | All rows |

## Methods

| Member | Signature | Notes |
|--------|-----------|-------|
| `mapAs<T>()` | `→ T[]` | Typed deserialisation — string-to-type coercion at boundary |

## Examples

```grob
type Employee {
    name:       string
    department: string
    salary:     int
}

table := csv.read(`C:\data\employees.csv`)
print("${table.rowCount} rows, headers: ${strings.join(table.headers, ", ")}")

employees := table.mapAs<Employee>()
```

See also: [CsvRow type](CsvRow.md), [csv module](../Standard-Library/csv.md)
