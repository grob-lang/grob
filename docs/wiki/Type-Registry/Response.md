# Response

Returned by `http.get()`, `http.post()`, `http.put()`, `http.patch()`,
`http.delete()`. Defined in `Grob.Http`.

| Member | Kind | Signature | Notes |
|--------|------|-----------|-------|
| `statusCode` | property | `→ int` | |
| `isSuccess` | property | `→ bool` | True if 200–299 |
| `headers` | property | `→ map<string, string>` | Keys normalised to lowercase |
| `asText()` | method | `→ string` | |
| `asJson()` | method | `→ json.Node` | Throws `JsonError` if not valid |
| `toString()` | method | `→ string` | Status summary |

See also: [Grob.Http](../Plugins/Grob-Http.md)
