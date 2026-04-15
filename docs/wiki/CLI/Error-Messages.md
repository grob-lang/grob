# Error Messages

## Design Principles

Error messages say what went wrong, where and why — in that order. Never blame
the developer. Suggest the fix when obvious.

```
Type error on line 14:
  Expected  int
  Got       string

  The function add() requires two int arguments.
  'name' is a string. Did you mean to convert it first?

  Hint: name.toInt() returns int? — check for nil before passing it.
```

Error messages show variable names and types, never values. `--verbose` overrides
for debugging.

See also: [Commands](Commands.md)
