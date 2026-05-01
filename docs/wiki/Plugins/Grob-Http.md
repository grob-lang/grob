# Grob.Http

First-party HTTP client plugin. Full REST support with authentication helpers.

## Install

```
grob install Grob.Http
```

## Import

```grob
import Grob.Http
// http.* and auth.* both available — auth is a sub-namespace, not a separate install
```

## Request Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| `http.get(url)` | `→ Response` | GET request |
| `http.get(url, auth)` | `→ Response` | GET with auth |
| `http.get(url, auth, headers: map)` | `→ Response` | GET with auth and headers |
| `http.post(url, body)` | `→ Response` | POST request |
| `http.post(url, body, auth)` | `→ Response` | POST with auth |
| `http.put(url, body, auth)` | `→ Response` | PUT with auth |
| `http.patch(url, body, auth)` | `→ Response` | PATCH with auth |
| `http.delete(url, auth)` | `→ Response` | DELETE (no body) |
| `http.download(url, destination)` | `→ void` | Stream file to disk |
| `http.download(url, destination, auth)` | `→ void` | Stream file with auth |

All request methods accept `timeoutSeconds: int = 30` as a named parameter.

## Response Type

| Member | Type | Description |
|--------|------|-------------|
| `statusCode` | `int` | HTTP status code |
| `isSuccess` | `bool` | True if 200–299 |
| `headers` | `map<string, string>` | Response headers |
| `asText()` | `→ string` | Body as text |
| `asJson()` | `→ json.Node` | Body as JSON (throws `JsonError` on parse failure) |

## Auth Helpers

| Function | Signature | Description |
|----------|-----------|-------------|
| `auth.bearer(token: string)` | `→ AuthHeader` | Bearer token |
| `auth.basic(username, password)` | `→ AuthHeader` | Basic authentication |
| `auth.apiKey(key, headerName: string = "X-Api-Key")` | `→ AuthHeader` | API key header |

`AuthHeader` is an opaque type — only `http.*` functions accept it. It is not
constructable directly.

## Examples

### Simple GET

```grob
import Grob.Http

response := http.get("https://api.example.com/status")
if (response.isSuccess) {
    print(response.asText())
}
```

### Authenticated API call

```grob
import Grob.Http

pat      := env.require("ADO_PAT")
response := http.get(
    "https://dev.azure.com/org/project/_apis/git/repositories",
    auth.bearer(pat)
)

repos := response.asJson().mapAs<Repo[]>()
repos.formatAs.table()
```

### POST with JSON body

```grob
import Grob.Http

body     := json.write(#{ title: "New issue", body: "Description" })
response := http.post("https://api.example.com/issues", body, auth.bearer(token))

if (!response.isSuccess) {
    log.error("Failed: ${response.statusCode}")
    exit(1)
}
```

### File download

```grob
import Grob.Http

http.download(
    "https://releases.example.com/tool-v2.zip",
    "C:\\Tools\\tool-v2.zip"
)
```

`http.download()` streams to disk — it does not load the response body into
memory. Use this for file downloads. For small text files,
`fs.writeText(path, http.get(url).asText())` is valid but not appropriate for
binary or large files.

### Custom timeout

```grob
import Grob.Http

response := http.get(url, auth.bearer(token), timeoutSeconds: 120)
```

## Error Behaviour

Request failures throw `NetworkError`. JSON parse failures on `asJson()` throw
`JsonError`. Timeout produces `NetworkError`.

See also: [Plugins Overview](Overview.md),
[Modules and Imports](../Language-Specification/Modules-and-Imports.md)
