# Grob.Http

HTTP client with auth helpers. First-party plugin.

```grob
import Grob.Http
```

## Functions

| Function | Signature |
|----------|-----------|
| `http.get(url, auth?, headers?, timeoutSeconds: int = 30)` | `→ Response` |
| `http.post(url, body, auth?, headers?, timeoutSeconds: int = 30)` | `→ Response` |
| `http.put(url, body, auth?, headers?, timeoutSeconds: int = 30)` | `→ Response` |
| `http.patch(url, body, auth?, headers?, timeoutSeconds: int = 30)` | `→ Response` |
| `http.delete(url, auth?, headers?, timeoutSeconds: int = 30)` | `→ Response` |
| `http.download(url, dest, auth?, timeoutSeconds: int = 30)` | `→ void` |

## Auth Helpers

| Function | Signature |
|----------|-----------|
| `auth.bearer(token: string)` | `→ AuthHeader` |
| `auth.basic(username, password: string)` | `→ AuthHeader` |
| `auth.apiKey(key, headerName: string = "X-Api-Key")` | `→ AuthHeader` |

## Examples

```grob
import Grob.Http

pat := env.require("ADO_PAT")
response := http.get(url, auth.bearer(pat))

if (response.isSuccess) {
    repos := response.asJson().mapAs<Repo>()
}
```

`body` is `string`. Serialise structs with `json.encode()` first.

See also: [Response](../Type-Registry/Response.md),
[AuthHeader](../Type-Registry/AuthHeader.md)
