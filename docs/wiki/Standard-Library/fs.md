# fs — File System

File system operations. Core module — auto-available, no import required.

## Module Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `fs.list(path, recursive: bool = false)` | `→ File[]` | List directory contents |
| `fs.exists(path: string)` | `→ bool` | True if file or directory exists |
| `fs.isFile(path: string)` | `→ bool` | True if path is a file |
| `fs.isDirectory(path: string)` | `→ bool` | True if path is a directory |
| `fs.ensureDir(path: string)` | `→ void` | Create full path; no-op if exists |
| `fs.createDir(path: string)` | `→ void` | Create directory; throws `IoError` if exists |
| `fs.delete(path: string)` | `→ void` | Delete file or empty directory |
| `fs.deleteRecursive(path: string)` | `→ void` | Delete directory and all contents |
| `fs.readText(path: string)` | `→ string` | Read file as UTF-8 text |
| `fs.readLines(path: string)` | `→ string[]` | Read file as lines |
| `fs.writeText(path: string, content: string)` | `→ void` | Create or overwrite file |
| `fs.appendText(path: string, content: string)` | `→ void` | Create or append to file |
| `fs.copy(src, dest, overwrite: bool = false)` | `→ void` | Copy by path |
| `fs.move(src, dest, overwrite: bool = false)` | `→ void` | Move by path |

## Examples

```grob
files := fs.list("C:\\Reports")
logs := files.filter(f => f.extension == ".log")

for file in logs {
    print("${file.name} — ${file.size} bytes")
}

content := fs.readText("C:\\config.json")
fs.writeText("C:\\output.txt", content)
fs.ensureDir("C:\\Reports\\2026\\April")
```

## Encoding

`fs.readText()` reads as UTF-8. BOM auto-detection is supported. `fs.writeText()`
writes UTF-8 without BOM. `fs.readLines()` splits on `\n` and `\r\n` transparently.

## `File` Type

See [File](../Type-Registry/File.md) in the Type Registry.

See also: [path](path.md), [json](json.md)
