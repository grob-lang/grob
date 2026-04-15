# Control Flow

## `if/else`

```grob
if (condition) {
    // then block
}

if (condition) {
    // then
} else if (other) {
    // else if
} else {
    // fallback
}
```

Parentheses around the condition are required. `else if` is two keywords — not
`elif`. `if/else` is a statement, not an expression. For expression-position
conditionals, use ternary `? :` or the switch expression.

## `while`

```grob
while (condition) {
    // body
}
```

Parentheses around the condition are required. `do...while` is deferred post-MVP.

## `select/case`

`select` is the multi-branch statement. First matching case executes. No
fall-through.

```grob
select (value) {
    case 0 {
        print("Zero")
    }
    case 1, 2 {
        print("One or two")
    }
    default {
        print("Something else: ${value}")
    }
}
```

`default` is optional. If omitted and no case matches, execution continues past
the `select` block. Works on any comparable type: `int`, `string`, `bool`.
`break` does not apply inside `select`.

## `for...in`

### Collection iteration

```grob
for file in files {
    print(file.name)
}

for i, file in files {
    print("${i}: ${file.name}")
}

for k, v in headers {
    print("${k}: ${v}")
}
```

The single-identifier form binds the value, not the index. Both identifiers are
declared by the `for` statement and are immutable within the body.

V1 supports three iterable types: numeric range, `T[]` array and `map<K, V>`.
The single-identifier form on a map is a compile error — use
`for k, v in myMap` or `for k in myMap.keys`.

### Numeric range

```grob
for i in 0..10 { }          // 0, 1, 2 ... 10 (inclusive)
for i in 0..100 step 5 { }  // 0, 5, 10 ... 100
for i in 10..0 step -1 { }  // 10, 9, 8 ... 0
```

`..` is inclusive on both bounds. `step` is optional (default `1`). A descending
range without an explicit negative step is a compile error.

## `break` and `continue`

```grob
for file in files {
    if (file.name == "skip.log") {
        continue
    }
    if (file.size > maxSize) {
        break
    }
    process(file)
}
```

Both are statements. Using either outside a loop is a compile error. Labelled
break is deferred post-MVP.

See also: [Expressions](Expressions.md), [Operators](Operators.md)
