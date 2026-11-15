# CsvRow — Type Registry

Represents a single row in a `csv.Table`.

## Methods

| Member | Signature | Notes |
|--------|-----------|-------|
| `get(name: string)` | `→ string` | By header name. Throws if no headers or name not found |
| `get(index: int)` | `→ string` | By zero-based index. Throws if out of range |

## Indexers

| Syntax | Signature | Notes |
|--------|-----------|-------|
| `row["Name"]` | `→ string` | Sugar for `get(name)` |
| `row[0]` | `→ string` | Sugar for `get(index)` |

## Examples

```grob
table := csv.read(`C:\data\report.csv`)

for row in table.rows {
    name   := row["Name"]
    email  := row["Email"]
    score  := row[2]
    print("${name} (${email}): ${score}")
}
```

See also: [CsvTable type](CsvTable.md), [csv module](../Standard-Library/csv.md)
