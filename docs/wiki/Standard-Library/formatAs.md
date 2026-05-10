# formatAs — Output Formatting

Human-readable output formatters. Core module — auto-available, no import
required. Distinct from `json.stdout()` which is machine-readable.

`formatAs` is a terminal operation on a collection: it takes a collection and
produces human-readable output. It is not a pipeline stage — it does not return
a new collection. Scalar formatting is handled by instance methods on the
relevant types (e.g. `myFloat.format("F2")`, `myDate.format("yyyy-MM-dd")`).

## Calling Convention

The canonical calling convention is chained from a collection:

```grob
results.formatAs.table()
results.formatAs.list()
results.formatAs.csv()
```

The compiler treats `.formatAs` as a known namespace prefix — not a runtime
property. At the call site the compiler rewrites to an internal call equivalent
to `formatAs.table(results)`. No runtime object is created, no boxing.

## Functions

| Function | Description |
|----------|-------------|
| `T[].formatAs.table()` | Auto-column table from struct fields |
| `T[].formatAs.table(columns: string[])` | Explicit column selection |
| `T[].formatAs.list()` | One item per line, key: value |
| `T[].formatAs.csv()` | CSV to stdout |

`formatAs.table()` derives column names from the type's field registry at compile
time. Works on named structs and anonymous structs.

## Examples

### Table output

```grob
type FileEntry {
    name:    string
    folder:  string
    size_mb: float
}

entries := fs.list("C:\\Reports").select(f => #{
    name:    f.name,
    folder:  f.folder,
    size_mb: f.size / 1048576.0
})

entries.formatAs.table()
```

### Column selection

```grob
entries.formatAs.table(["name", "size_mb"])
```

### List output

```grob
entries.formatAs.list()
```

Output:

```
name: quarterly-report.pdf
folder: C:\Reports
size_mb: 1.24

name: budget-2026.xlsx
folder: C:\Reports
size_mb: 0.87
```

### CSV output

```grob
entries.formatAs.csv()
```

Writes RFC 4180 CSV to stdout, including a header row.

## Notes

- Output goes to stdout.
- `formatAs` is a core module and does not require `import`.
- For machine-readable JSON output, use `json.stdout()`.
- For scalar formatting (dates, numbers), use the instance methods on the
  relevant types.

*Renamed from `format` to `formatAs`, D-282, April 2026.*
