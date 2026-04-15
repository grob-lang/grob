# json — JSON Parse and Serialise

JSON operations. Core module — auto-available, no import required.

## Module Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `json.read(path: string)` | `→ json.Node` | Read and parse JSON file |
| `json.write(path, value: T, compact: bool = false)` | `→ void` | Write value as JSON file |
| `json.parse(content: string)` | `→ json.Node` | Parse JSON string |
| `json.encode(value: T, compact: bool = false)` | `→ string` | Serialise value to JSON string |
| `json.stdin()` | `→ json.Node` | Read JSON from stdin |
| `json.stdout(value: T, compact: bool = false)` | `→ void` | Write JSON to stdout |

`json.write()` and `json.encode()` default to pretty-printed output. Pass
`compact: true` for single-line output.

## Examples

```grob
// Read and deserialise
repos := json.read("C:\\data\\repos.json").mapAs<Repo>()

// Serialise and write
json.write("C:\\output\\results.json", results)

// Parse process output
node := json.parse(response.asText())
name := node["name"].asString()

// Nil-safe navigation
count := node["missing"]?.asInt() ?? 0
```

## `json.Node`

See [json.Node](../Type-Registry/json-Node.md) in the Type Registry.

See also: [csv](csv.md), [Grob.Http](../Plugins/Grob-Http.md)
