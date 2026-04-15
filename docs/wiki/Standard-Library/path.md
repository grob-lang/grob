# path — Path Manipulation

Path string manipulation. Core module — auto-available, no import required.
No file system I/O — operates on strings only.

## Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `path.join(parts: string...)` | `→ string` | Variadic, OS separator |
| `path.joinAll(parts: string[])` | `→ string` | Array form |
| `path.extension(p: string)` | `→ string` | Lowercased, includes dot |
| `path.filename(p: string)` | `→ string` | Final segment with extension |
| `path.stem(p: string)` | `→ string` | Final segment without extension |
| `path.directory(p: string)` | `→ string` | Parent directory |
| `path.resolve(p: string)` | `→ string` | Absolute path relative to CWD |
| `path.normalise(p: string)` | `→ string` | OS separator, collapse `..` |
| `path.isAbsolute(p: string)` | `→ bool` | |
| `path.isRelative(p: string)` | `→ bool` | |
| `path.changeExtension(p, ext: string)` | `→ string` | ext should include dot |
| `path.separator` | `→ string` | OS-dependent: `\` on Windows |

## Examples

```grob
full := path.join("C:\\Reports", "2026", "April", "report.csv")
ext  := path.extension("report.xlsx")   // ".xlsx"
dir  := path.directory("C:\\Reports\\file.txt")  // "C:\\Reports"
```

See also: [fs](fs.md)
