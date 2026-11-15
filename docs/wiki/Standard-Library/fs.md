# fs — File System

File system operations. Core module — auto-available, no import required.

## Module Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `fs.list(path, recursive: bool = false)` | `→ File[]` | List files and directories |
| `fs.exists(path: string)` | `→ bool` | Check if path exists |
| `fs.isFile(path: string)` | `→ bool` | Check if path is a file |
| `fs.isDirectory(path: string)` | `→ bool` | Check if path is a directory |
| `fs.ensureDir(path: string)` | `→ void` | Create directory recursively, no-op if exists |
| `fs.createDir(path: string)` | `→ void` | Create directory, throws `IoError` if exists |
| `fs.delete(path: string)` | `→ void` | Delete file or empty directory |
| `fs.deleteRecursive(path: string)` | `→ void` | Delete directory and all contents |
| `fs.readText(path: string)` | `→ string` | Read entire file as text |
| `fs.readLines(path: string)` | `→ string[]` | Read file as array of lines |
| `fs.writeText(path: string, content: string)` | `→ void` | Create or overwrite file |
| `fs.appendText(path: string, content: string)` | `→ void` | Create or append to file |
| `fs.copy(src: string, dest: string)` | `→ void` | Copy file by path |
| `fs.move(src: string, dest: string)` | `→ void` | Move file by path |

`fs.delete()` refuses a non-empty directory and throws `IoError`.
`fs.deleteRecursive()` makes the destructive intent explicit at the call site.
Two levels of delete by design.

## The `File` Type

`fs.list()` returns `File[]`. `File` is a built-in type registered at startup.
See [File type registry](../Type-Registry/File.md) for all members.

## Examples

### List and filter files

```grob
files := fs.list(`C:\Reports`, recursive: true)
    .filter(f => f.extension == ".xlsx")
    .sort(f => f.modified, descending: true)

for file in files {
    print("${file.name}  ${file.size} bytes")
}
```

### Read, transform, write

```grob
content := fs.readText(`C:\config.json`)
config  := json.parse(content)

// Process...

fs.writeText(`C:\output.json`, json.write(config))
```

### Safe directory creation

```grob
dest := path.join(`C:\Archive`, "2026", "April")
fs.ensureDir(dest)

for file in fs.list(`C:\Reports`) {
    file.moveTo(dest)
}
```

## Error Behaviour

All I/O functions throw `IoError` on failure (file not found, permission denied,
disk full). The compiler/type checker does not verify file existence — these are
runtime errors by nature.
