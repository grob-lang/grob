# csv — CSV Parse and Serialise

CSV operations. Core module — auto-available, no import required.

## Module Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `csv.read(path, hasHeaders: bool = true, delimiter: string = ",")` | `→ csv.Table` | Read CSV file |
| `csv.parse(content, hasHeaders: bool = true, delimiter: string = ",")` | `→ csv.Table` | Parse CSV string |
| `csv.write(path, rows: T[], hasHeaders: bool = true, delimiter: string = ",")` | `→ void` | Write CSV file |
| `csv.stdin(hasHeaders: bool = true, delimiter: string = ",")` | `→ csv.Table` | Read CSV from stdin |
| `csv.stdout(rows: T[], hasHeaders: bool = true, delimiter: string = ",")` | `→ void` | Write CSV to stdout |

## Examples

```grob
table := csv.read("C:\\data\\employees.csv")
employees := table.mapAs<Employee>()

// TSV
table := csv.read("C:\\data\\export.tsv", delimiter: "\t")

// Write results
csv.write("C:\\output\\report.csv", results)
```

RFC 4180 compliant: quoted fields, embedded commas, embedded newlines and `""`
escape for double-quote.

See also: [CsvTable](../Type-Registry/CsvTable.md),
[CsvRow](../Type-Registry/CsvRow.md)
