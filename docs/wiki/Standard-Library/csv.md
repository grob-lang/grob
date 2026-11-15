# csv — CSV Parse and Serialise

CSV reading, writing, parsing and pipeline I/O. Core module — auto-available,
no import required. RFC 4180 compliant.

## Module Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `csv.read(path, hasHeaders: bool = true, delimiter: string = ",")` | `→ csv.Table` | Read a CSV file |
| `csv.parse(content, hasHeaders: bool = true, delimiter: string = ",")` | `→ csv.Table` | Parse a CSV string |
| `csv.stdin(hasHeaders: bool = true, delimiter: string = ",")` | `→ csv.Table` | Read CSV from stdin |
| `csv.write(path, rows: T[], hasHeaders: bool = true, delimiter: string = ",")` | `→ void` | Write to CSV file |
| `csv.stdout(rows: T[], hasHeaders: bool = true, delimiter: string = ",")` | `→ void` | Write CSV to stdout |

## The `csv.Table` Type

See [CsvTable type registry](../Type-Registry/CsvTable.md).

| Member | Kind | Signature | Description |
|--------|------|-----------|-------------|
| `headers` | property | `→ string[]` | Column headers (empty if `hasHeaders: false`) |
| `rowCount` | property | `→ int` | Number of data rows |
| `rows` | property | `→ CsvRow[]` | All rows |
| `mapAs<T>()` | method | `→ T[]` | Typed deserialisation |

## Examples

### Read and process

```grob
type Employee {
    name:       string
    department: string
    salary:     int
}

employees := csv.read(`C:\data\employees.csv`).mapAs<Employee>()

for emp in employees {
    print("${emp.name} — ${emp.department}")
}
```

### Tab-delimited file

```grob
data := csv.read(`C:\data\export.tsv`, delimiter: "\t")
```

### Raw row access

```grob
table := csv.read(`C:\data\report.csv`)

for row in table.rows {
    name   := row["Name"]
    salary := row["Salary"]
    print("${name}: ${salary}")
}
```

### Write results

```grob fragment
csv.write(`C:\output\results.csv`, results)
```

## Error Behaviour

`csv.read()` throws `IoError` if the file cannot be read. `csv.parse()` and
`csv.read()` throw `RuntimeError` on malformed CSV. `mapAs<T>()` throws
`RuntimeError` if field names do not match the target type.

## RFC 4180 Compliance

Quoted fields, embedded commas, embedded newlines and `""` escape for
double-quote are all supported.
