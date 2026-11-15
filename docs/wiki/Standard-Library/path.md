# path — Path String Manipulation

Path string operations — join, decompose, normalise. Core module —
auto-available, no import required. No file system I/O — `path` operates on
strings only.

## Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `path.join(parts: string...)` | `→ string` | Join segments with OS separator |
| `path.joinAll(parts: string[])` | `→ string` | Join array of segments |
| `path.extension(p: string)` | `→ string` | Lowercased, includes dot. Empty if none |
| `path.filename(p: string)` | `→ string` | Final segment including extension |
| `path.stem(p: string)` | `→ string` | Final segment without extension |
| `path.directory(p: string)` | `→ string` | Parent directory portion |
| `path.resolve(p: string)` | `→ string` | Absolute path relative to CWD |
| `path.normalise(p: string)` | `→ string` | OS separator, collapse `..` and `.` |
| `path.isAbsolute(p: string)` | `→ bool` | |
| `path.isRelative(p: string)` | `→ bool` | |
| `path.changeExtension(p: string, ext: string)` | `→ string` | Ext should include dot |

## Constants

| Constant | Type | Description |
|----------|------|-------------|
| `path.separator` | `string` | `\` on Windows, `/` on POSIX |

## Examples

### Build output path

```grob fragment
dest := path.join("C:\\Archive", year, month, file.name)
```

### Variadic join

```grob
output := path.join("C:\\Reports", "2026", "Q1", "summary.xlsx")
// "C:\\Reports\\2026\\Q1\\summary.xlsx"
```

### Decompose a path

```grob
p := "C:\\Reports\\2026\\summary.xlsx"

path.filename(p)     // "summary.xlsx"
path.stem(p)         // "summary"
path.extension(p)    // ".xlsx"
path.directory(p)    // "C:\\Reports\\2026"
```

### Change extension

```grob
csv_path := path.changeExtension("C:\\data\\report.xlsx", ".csv")
// "C:\\data\\report.csv"
```

### Normalise mixed separators

```grob
clean := path.normalise("C:/Users\\chris/./documents/../downloads")
// "C:\\Users\\chris\\downloads"
```

## Relationship to `File` Type

`File` type properties (`file.extension`, `file.directory` etc) are convenience
accessors on known file objects. `path.*` functions operate on arbitrary path
strings from any source — process output, config files, user input, environment
variables. Both exist because both are needed.
