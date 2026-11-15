# json — JSON Parse and Serialise

JSON reading, writing, parsing and pipeline I/O. Core module — auto-available,
no import required.

## Module Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `json.read(path: string)` | `→ json.Node` | Read and parse a JSON file |
| `json.write(path: string, value: any)` | `→ void` | Serialise and write to file |
| `json.parse(content: string)` | `→ json.Node` | Parse a JSON string |
| `json.stdin()` | `→ json.Node` | Read and parse JSON from stdin |
| `json.stdout(value: any)` | `→ void` | Serialise and write to stdout |

`json.read()` and `json.write()` are the primary forms — file-based,
Windows-native, no shell piping required. `json.stdin()` and `json.stdout()`
exist for pipeline composition and agent use cases.

## The `json.Node` Type

`json.Node` represents a parsed JSON value. Navigate with indexers and convert
with typed accessors:

```grob
node := json.read(`C:\data\repos.json`)

name  := node["name"].asString()
count := node["count"].asInt()
items := node["items"].asArray()
```

## Typed Deserialisation

`mapAs<T>()` maps JSON to user-defined types:

```grob
type Repo {
    name:    string
    ssh_url: string
}

repos := json.read(`C:\data\repos.json`).mapAs<Repo[]>()

for repo in repos {
    print(repo.name)
}
```

## Examples

### Read, filter, write

```grob
type Task {
    id:       int
    title:    string
    complete: bool
}

tasks := json.read(`C:\tasks.json`).mapAs<Task[]>()
pending := tasks.filter(t => !t.complete)
json.write(`C:\pending.json`, pending)
```

### Parse process output

```grob
result := process.run("az", ["group", "list", "--output", "json"])
groups := json.parse(result.stdout).mapAs<AzGroup[]>()
```

### Pipeline entry and exit

```grob
// Read JSON from stdin, process, write JSON to stdout
repos := json.stdin().mapAs<Repo[]>()
stale := repos.filter(r => r.stale_count > 0)
json.stdout(stale)
```

## Error Behaviour

`json.read()` throws `IoError` if the file cannot be read. `json.parse()` and
`json.read()` throw `JsonError` if the content is not valid JSON. `mapAs<T>()`
throws `JsonError` if the JSON structure does not match the target type.
