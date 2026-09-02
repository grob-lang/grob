# Grob — Formatter Specification

> Specification for `grob fmt`. Implementation reference for Sprint 12.
> Authored in Session D Part 2, April 2026.
> When this document and the decisions log conflict, the decisions log wins.

-----

## 1. Foundational Principle

`grob fmt` is a whitespace and bracket normaliser. Its job is to take valid
Grob source and emit canonical Grob source — identical in meaning, different
only in shape.

Five properties define the formatter:

- **One opinion.** No configuration files. No project-level overrides. No
  style flags. One formatter, one output.
- **On demand.** `grob fmt <file>` runs when the developer invokes it. Never
  automatic. Never on save. Never on commit.
- **Anti-ceremony.** Grob's identity is against the tax of configuration.
  That principle applies to the formatter too.
- **Idempotent.** `fmt(fmt(x)) == fmt(x)`. Running the formatter twice
  produces the same output as running it once.
- **Semantically transparent.** The formatter changes how code looks, never
  what it means.

Users will occasionally disagree with individual rules. That is the cost of
opinionation. Anyone who wants a formatter they can configure can write a
different tool.

-----

## 2. Behavioural Contract

`grob fmt` reads valid Grob source and writes canonical Grob source. The two
are equivalent under compilation — the parsed AST is unchanged, the type
checker's conclusions are unchanged, the bytecode emitted is unchanged.

The formatter operates on the parsed AST, not the token stream. This is an
implementation constraint, not a behaviour rule, but it has consequences:
rules that require semantic context (distinguishing unary minus from binary
subtraction, distinguishing a namespace call from a method call) are
expressible only because the AST has already resolved them.

The formatter shares its lexer, parser, and diagnostic machinery with the
compiler. A file that parses cleanly for `grob run` parses cleanly for
`grob fmt`. A parse error produces the same diagnostic from both.

-----

## 3. Rules

The rules that follow are grouped by construct. Each rule is mechanically
applicable by a parser-aware tool. Rules are stated concisely; rationale is
brief where the rule is obvious and deeper where it is not.

-----

### 3.1. Indentation

Four spaces per indent level. Never tabs.

Content of single-backtick and triple-backtick string literals is preserved
verbatim, including tabs, internal newlines, and any leading whitespace
within.

Multi-line bracketed constructs (function calls, function signatures, array
literals, map literals, struct literals, `type` declarations, `param` declarations,
method chains, switch expression arms) indent their contents one level from
the opening line.

```grob
// Four spaces, not a tab
if (x > 0) {
    print("positive")
}

// Content of a triple-backtick string is sacred
query := ```
    SELECT *
        FROM users
    WHERE active = 1
```
```

Rationale: alignment (see §3.8) only works reliably with spaces; tabs and
alignment fight each other. Raw string content is intent, not style; the
formatter has no authority to change string values.

-----

### 3.2. Line Length

Target line length is 100 columns.

The formatter wraps a line when its single-line form exceeds 100 columns and
the line contains at least one wrappable bracketed construct. It collapses a
multi-line construct to a single line when the collapsed form would fit
within 100 columns.

A line can exceed 100 columns when no wrappable bracket exists. Long binary
expressions, long string literals of any form, long comments, and long
identifiers are not wrapped.

Wrappable constructs are:

- Function call argument lists
- Function signature parameter lists (subject to the 3+ parameter threshold
  in §3.11)
- Array literals
- Map literals
- Struct literals (anonymous `#{ ... }` and named `TypeName { ... }`)
- `type` field declarations (always multi-line — see §3.8)
- Switch-expression arms (D-421 — §3.1 already indents them as a multi-line
  bracketed construct; listed here so §3.5's Category B membership has a
  referent)
- Method chains (subject to the receiver rule in §3.9)

The wrap rule is symmetric: if a single-line form fits within 100 columns,
it is emitted; if not, the multi-line form is emitted. The user cannot lock
a preference for either form. This keeps the formatter idempotent and
predictable.

```grob
// Under 100 — single-line
result := someFunction(a, b, c)

// Over 100 — wraps
result := someFunction(
    a_very_long_argument,
    another_long_one,
    yet_another,
    plus_one_more,
)
```

-----

### 3.3. Blank Lines

**Between top-level elements of different kinds** (imports, params, types,
fns, top-level consts, script body): one blank line.

**Between top-level declarations of the same kind** (consecutive `fn`s,
consecutive `type`s): one blank line.

**Within same-kind declaration blocks** (consecutive `import`s within a
group, consecutive `param`s within a group, consecutive top-level `const`s,
consecutive top-level `readonly`s): no blank lines inserted, and none
removed — see the group rules below for `import` and `param`.

`const` and `readonly` form **separate** same-kind blocks. A run of
`const` declarations followed directly by a run of `readonly` declarations
is two blocks, separated by one blank line under the "between top-level
declarations of different kinds" rule.

**Between import groups** (groups defined by user-authored blank-line
separators): exactly one blank line.

**Between `param` formatting groups** (groups defined by user-authored
blank-line separators, exactly as for imports): exactly one blank line. The
formatter preserves the author's grouping and never merges two groups into one
(D-413). These are formatting groups only — not the contiguity-delimited
parameter group §19 of `grob-language-fundamentals.md` uses for ordering; see
§3.10.

**Around a decorated `param` declaration:** exactly one blank line above the
decorator stack and one below the declaration, inserted by the formatter if
absent (D-413). This is the one blank line `grob fmt` adds on its own
initiative, and it exists so a decorator's scope can never be misread as
extending to the declaration below it.

**Inside function and block bodies:** user blank lines are preserved;
sequences of two or more blank lines collapse to one.

**At block boundaries:** no blank line immediately after an opening `{`,
no blank line immediately before a closing `}`.

**Trailing whitespace on every line is stripped**, regardless of context.

```grob
import Grob.Http
import Grob.Crypto

import Company.Azure.Sdk

param token:  string
param region: string = "uksouth"

type Config {
    host: string
    port: int = 8080
}

fn main(): void {
    setup()

    run()
}
```

-----

### 3.4. Brace Placement

The opening brace sits on the same line as the preceding statement token
with exactly one space separator:

```grob
if (condition) {
while (condition) {
fn foo(x: int): int {
type Repo {
```

Allman-style braces (opening brace on its own line) are a grammar
violation, not a formatter concern — the parser rejects them before the
formatter sees the input.

The closing brace sits on its own line at the column of the first
non-whitespace character of the opening statement.

**Continuation keywords** (`else`, `else if`, `catch`, `finally`) share a
line with the closing brace of the preceding block and the opening brace
of the next block. One space either side of each keyword.

```grob
if (x > 0) {
    positive()
} else if (x < 0) {
    negative()
} else {
    zero()
}

try {
    dangerous()
} catch (IoError e) {
    handle(e)
} finally {
    cleanup()
}
```

**Empty blocks and empty literals** render as `{ }` with exactly one space
between the braces, regardless of whether the context is a block, a struct
literal, a map literal, or an empty function body.

```grob
fn noop(): void { }

c := Config { }
```

-----

### 3.5. Separator Normalisation

Grob uses three separator conventions, distinguished by construct category.
The enumeration below is **complete** — every comma-or-newline-separated
construct in the language appears in exactly one category (D-421).

**Category A — declaration lists.** `type` field declarations and `param`
declarations use newline-only separation. The formatter emits no commas
between field or parameter declarations; if commas are present in the input,
the formatter removes them.

```grob
type FileEntry {
    name:    string
    folder:  string
    size_mb: float
}
```

**Category B — wrappable value lists.** Function call arguments, function
signature parameters, array literals, map literals, named type construction,
anonymous struct literals, and switch-expression arms use comma separation
and are wrappable under §3.2. Multi-line forms have a trailing comma after
every element including the last. Single-line forms have no trailing comma.

A call with a **single** argument that wraps is still a multi-line argument
list, and its one argument takes the trailing comma. The §6 worked example is
the canonical instance.

```grob
// Multi-line — trailing comma on every element
p := Person {
    name:    "Chris",
    address: Address { city: "London", country: "UK" },
}

// Single-line — no trailing comma
nums := [1, 2, 3]

// One wrapped argument — still a trailing comma
print(
    entries.sort(e => e.size_mb, descending: true).formatAs.table(),
)
```

**Category C — comma-separated, never wrapped.** Lambda parameter lists,
function-type parameter lists (`fn(int, string): bool`), generic and map
type-argument lists (`map<K, V>`, `mapAs<T>`), and `select` case pattern
lists are comma-separated but are **not** wrappable constructs under §3.2,
so the formatter never produces a multi-line form of one. They are therefore
always emitted single-line with no trailing comma. The grammar permits a
trailing comma in all four (§16); the formatter removes it.

```grob
f := (a: int, b: int) => a + b
rows := table.mapAs<Employee>()
select (code) {
    case 200, 201 { print("ok") }
}
```

**Not a list.** `for k, v in map { }` uses a comma to separate a fixed pair
of loop variables. It is not a separator convention and the formatter takes
no action on it beyond §3.6 spacing.

**Spacing around commas:** one space after, no space before. No exceptions.

**Semicolons** are not part of Grob grammar. The formatter takes no action
on them.

-----

### 3.6. Operator Spacing

Every operator site has a defined rule. The table below is complete; any
operator not listed does not exist in Grob.

| Site | Rule |
|---|---|
| Binary arithmetic (`+`, `-`, `*`, `/`, `%`) | Space either side |
| Comparison (`==`, `!=`, `<`, `>`, `<=`, `>=`) | Space either side |
| Logical (`&&`, `\|\|`) | Space either side |
| Declaration (`:=`) | Space either side |
| Assignment (`=`) | Space either side |
| Compound assignment (`+=`, `-=`, `*=`, `/=`, `%=`) | Space either side |
| Default value `=` in param or field | Space either side |
| Ternary `?` and `:` | Space either side of each |
| Lambda arrow `=>` | Space either side |
| Nil coalesce `??` | Space either side |
| Type annotation `:` (locals, params, return types, map keys, named args) | No space before, one space after |
| Unary `-`, `!` | No space before operand |
| Postfix `++`, `--` | No space before operator |
| Range `..` | No spaces |
| Optional chain `?.` | No spaces |
| Member access `.` | No spaces |
| Nullable suffix `?` | No space before |
| Array type suffix `[]`, `[][]` | No spaces |
| Generic parameters `<T>` | No spaces inside or adjacent |
| Interpolation `${ }` | No inner spaces |
| Indexing `[ ]` | No inner spaces |
| Call parens `( )` | No inner spaces |
| Decorator `@name(...)` | No space after `@`, no space before `(` |
| Control-flow condition parens | No inner space, one space before `{` |
| Commas | No space before, one space after |

```grob
x := a + b * c
items.filter(f => f.size > threshold)
name: string = "default"
result := values[0].toString()
value ?? "fallback"
negated := -x
count++
range := 0..10
user?.name
```

**Implementation constraint:** the formatter operates on the parsed AST.
Token-level formatting cannot distinguish unary minus from binary
subtraction, nor postfix `++` from two unary expressions — both require
semantic context the parser provides.

-----

### 3.7. String Literals

String literal content is never modified. Double-quoted, single-backtick,
and triple-backtick string content is emitted verbatim, including all
whitespace, all escape sequences, and all internal characters.

The formatter does not convert between string literal forms. `"..."`,
`` `...` ``, and `` ```...``` `` are three distinct expressions of
developer intent. The convention that backtick raw strings are canonical
for Windows paths is taught through samples and documentation; it is not
enforced by the formatter.

The formatter does not convert string concatenation (`"Hello " + name`)
into interpolation (`"Hello ${name}"`), or vice versa.

**Interpolation expressions inside `${...}` are reformatted** using the
same rules the formatter applies to any other expression. The `${` and
`}` delimiters are tight (no inner space). String content outside the
`${...}` delimiters is verbatim.

```grob
// Content untouched — interpolation expression reformatted
greeting := "Hello ${user.name.toUpper()}, you have ${count + 1} messages"

// Form is user intent — formatter does not convert
path := `C:\Users\Chris\Downloads`     // stays
path := "C:\\Users\\Chris\\Downloads"  // stays
```

**Implementation note:** string literals are token-granularity for the
formatter — emit the token unchanged. The interpolation expression inside
`${...}` is an AST subtree and is re-emitted normally.

-----

### 3.8. Struct and Map Literals, Type Declarations, and Alignment

**Single-line versus multi-line** is driven by §3.2's wrap rule.

**Empty:** `TypeName { }` and `#{ }` with one space between braces (per
§3.4).

**Single-line non-empty:** one space after `{`, one space before `}`.
Fields or entries separated by `, ` (comma + space) per §3.5.

**Multi-line:** `{` at end of opening line. One field per line, indented
one level. Closing `}` on its own line at the column of the opening
statement's first token.

**Alignment** is the defining stylistic rule for Grob's declarative
constructs. `:` is aligned across the following, in multi-line form where
the construct has one:

1. **`type` field declarations.** `:` aligned one space after the longest
   field name.
2. **`param` declarations.** `:` aligned one space after the longest param
   name in the group, **and** `=` aligned one space after the longest type
   annotation in the group. Groups are delimited by the author's blank
   lines, and a decorated declaration is always alone in its group
   (D-413; see §3.10).
3. **Named type construction** (`Person { ... }`) and **anonymous struct
   literals** (`#{ ... }`). `:` aligned one space after the longest field
   name.

**Alignment scope.** For the braced constructs above — `type`
declarations, named type construction and anonymous struct literals —
alignment is computed independently within each brace pair. Two adjacent
type declarations do not align to a shared column; each type aligns within
itself. **`param` declarations have no brace pair** (D-410 retired the
`param { ... }` form), so their alignment scope is the formatting group
defined in §3.10 — a run of declarations delimited by blank lines. The
principle is the same in both cases: alignment never reaches across an
independent scope.

**No alignment** is applied in:

- Map literals (`{ "host": "localhost", "port": 8080 }`)
- Named arguments in function calls
- Single-line forms of any construct

```grob
// Multi-line type declaration — ':' aligned
type Employee {
    name:       string
    job_title:  string
    department: string
    salary:     int
    start_date: string
}

// Multi-line named construction — ':' aligned
e := Employee {
    name:       "Jordan",
    job_title:  "Engineer",
    department: "Platform",
    salary:     65000,
    start_date: "2024-03-01",
}

// Map literal — no alignment
config := #{
    "host": "localhost",
    "port": 8080,
    "database_name": "production",
}

// Single-line — no alignment
p := Point { x: 3, y: 4 }
```

Rationale for alignment: the sample scripts — authored before the
formatter was specified — align `:` consistently. That is the canonical
Grob aesthetic. The standard argument against alignment (diff churn on
field renames) is weaker in a scripting language whose primary use is
short single-author files. The readability benefit is constant; the
churn cost is occasional and visible.

-----

### 3.9. Method Chains

**Single-line when fits** within 100 columns. Otherwise, multi-line.

**Multi-line form:** the **receiver construction** occupies the first
line. Every method call on the chain, starting from the first, breaks
onto its own line, indented one level from the receiver, with a leading
`.`.

The receiver construction is the expression before the first method call
that operates on the receiver's result:

- A bare identifier (`items`).
- A namespace call (`csv.read(file)`, `fs.list(path)`, `http.get(url)`).
- A constructor (`Person { ... }`).
- Another primary expression.

The formatter distinguishes receiver construction from method calls via
the AST — `csv` resolves to a module; `x.foo()` on a value is a method
call. The type checker's `ResolvedType` annotations (required day-one per
D-137) provide this information.

**Leading-dot style is mandatory** in multi-line form. Trailing-dot style
is also valid grammar (Language Fundamentals §14) but the formatter
normalises to leading-dot.

```grob
// Short chain — single-line
count := items.filter(f => f.active).count()

// Long chain on a bare identifier — chain on subsequent lines
issues
    .filter(i => date.parse(i.created_at) < cutoff)
    .select(i => #{
        number: i.number,
        title:  i.title,
        age:    date.parse(i.created_at).daysUntil(date.today()),
        author: i.user.login,
    })
    .formatAs.table()

// Long chain on a namespace call — receiver construction on line 1
csv.read(input_file)
    .mapAs<Employee>()
    .filter(e => e.department == department && e.salary < max_salary)
    .select(e => #{ name: e.name, salary: e.salary })
    .sort(e => e.salary, descending: true)
    .formatAs.table()
```

**Nested lambda bodies** inside chain calls follow §3.4 and §3.8
recursively. A multi-line struct literal inside a `.select(...)` call
closes with `})` on its own line at the chain call's indent.

**A receiver construction that itself exceeds 100 columns** wraps
internally per §3.2; the chain then begins on the line after the
receiver's closing bracket, indented one level from the receiver's
opening line.

-----

### 3.10. Parameter Declarations

**One param per line.** The formatter never merges params onto a single
line, regardless of length.

**Grouping is the author's (D-413).** A run of `param` declarations
uninterrupted by a blank line is one **formatting group**. The formatter
preserves the author's blank lines, collapses runs of two or more to one, and
never inserts a blank line between two undecorated declarations. This is the
same mechanism §3.3 already applies to import groups. A parameter list of any
length is a reading problem before it is a formatting problem, and the
author is the only one who knows which parameters belong together.

**A formatting group is not §19's parameter group.** `grob fmt`'s group is
delimited by blank lines and scopes nothing but alignment columns and
blank-line handling. The **parameter group** of
`grob-language-fundamentals.md` §19 is delimited by **contiguity**: it ends at
the first significant line that is neither a `param` declaration nor one of
its decorators, and a blank line does not end it. The two are deliberately
different — a script's params may form several formatting groups and still be
one parameter group, so the blank lines this section requires never turn a
later `param` into an E2202 ordering violation. Do not apply either rule in
the other's place.

**Decorated declarations are isolated (D-413).** Exactly one blank line sits
above a decorator stack and one below the declaration it decorates, inserted
by the formatter if the author omitted it. This is the sole blank line
`grob fmt` adds unprompted, and it is not removable. Its purpose is
correctness of reading, not tidiness: without it,

```grob
@secure
param token: string
param days:  int = 30
```

invites the reader to wonder whether `days` is also secure. A decorator
stack bounded by whitespace above and below cannot be misread. A decorated
declaration is therefore always alone in its group.

**Alignment is computed per group.** `:` aligned one space after the longest
param name in the group; `=` aligned one space after the longest type
annotation in the group. Params without a default contribute to the `=`
column only indirectly — the column is set by the longest type, not the
longest type-with-default. Because each group aligns independently, the
author's grouping is reinforced by visibly distinct columns rather than
flattened into one.

**Decorators** on their own line above the param. Multiple decorators stack
vertically, no blank lines within the stack, no blank line between the last
decorator and the param.

**The formatter never reorders params.** See §3.14.

```grob
@secure
param token: string

param source_dir: string
param dest_dir:   string

param dry_run:      bool = false
param max_parallel: int  = 4
```

Three groups, three independent columns, the decorated declaration isolated
by construction.

-----

### 3.11. Function Signatures

**Single-line when fits** within 100 columns **or parameter count is
fewer than 3.** Signatures with 0, 1, or 2 parameters stay single-line
regardless of length. Long signatures at these widths are a developer
concern; wrapping them implies structural significance that a two-param
function does not carry.

**Multi-line form** (3 or more parameters, over 100 columns): one
parameter per line, trailing comma on every parameter including the last,
closing `)` on its own line at the column of the `fn` keyword, return
type annotation `: Type` after the closing paren, same-line `{`.

**Alignment in multi-line form** follows the same rule as `param`
declarations (§3.10). `:` aligns one space after the longest parameter name.
`=` aligns one space after the longest type annotation.

**Single-line form:** no alignment. Standard spacing per §3.6.

**Empty parameter list:** `fn foo(): T { }` — no inner spaces in the
parens, one space between braces per §3.4.

```grob
// 0 params — single-line
fn noop(): void { }

// 2 params, long — stays single-line (threshold rule)
fn compute_weighted_average(values: float[], weights: float[]): float { }

// 3 params, fits — single-line
fn add_three(a: int, b: int, c: int): int { }

// 4 params, over 100 — multi-line with aligned ':' and '='
fn send_request(
    url:     string,
    method:  string              = "GET",
    timeout: int                 = 30,
    headers: map<string, string> = #{},
): Response { }
```

-----

### 3.12. Comments

**Comment content is verbatim.** The formatter never modifies the text of
a comment. No spell correction, no capitalisation, no rewrapping.

**`//` and `///` line comments:** exactly one space between the marker
and the content. Leading whitespace inside the comment beyond that first
space is stripped.

**Empty comments:** `//` on its own line with no trailing whitespace.
Preserved as a deliberate marker.

**Inline comments:** at least two spaces between the preceding code and
`//`, collapsed from more and added when fewer. One space between `//`
and the content.

**Block comments `/* */`:** content is verbatim, including internal
indentation and internal line breaks. The opening `/*` sits at the
indent of the surrounding code; the closing `*/` stays wherever the user
placed it.

**Doc comments `///`** are stylistically identical to `//` line comments
in v1. When doc comments gain semantics post-MVP, this rule may evolve.

**Comments inside multi-line aligned constructs:** the comment sits two
spaces after the content on its line. No cross-line alignment of `//`
columns.

```grob
// One space after '//', strip extras
x := 42  // inline comment sits two spaces after code

/* Block comment
   internal indentation preserved
   closing marker stays where the user put it */

type Employee {
    name:       string
    job_title:  string  // renamed from 'title'
    department: string
}
```

-----

### 3.13. Imports

**Order preserved.** The formatter does not sort imports, either across
the block or within groups. The order the developer wrote is the order
emitted.

**Grouping preserved.** Blank-line separators between imports in the
source are treated as group markers. The formatter preserves the group
structure.

**Within a group:** no blank lines. Consecutive imports are packed.

**Between groups:** exactly one blank line. Multiple blanks collapse to
one.

**No alignment** of the `as` keyword or alias identifier across imports.

**Import statement shape:** single line. Never wrapped.

**No deduplication.** The formatter does not remove duplicate or unused
imports. Those are compiler concerns.

```grob
import Grob.Http
import Grob.Crypto
import Grob.Zip

import Company.Azure.Sdk
import Company.Github.Api as gh

import Community.SlackTools
```

Rationale: Grob imports are plugins — external runtime dependencies that
the script author groups semantically (first-party / company-internal /
community). Sorting alphabetically destroys that grouping. The
determinism argument that motivates sort-imports in multi-author
application codebases applies weakly to single-author scripts.

-----

### 3.14. Declaration and Parameter Order

**`grob fmt` never reorders declarations or parameters.** Not `param`
declarations, not function signature parameters, not `type` fields, not
`import` statements within a group, not top-level declarations of any kind.
Formatting changes whitespace; it never changes what the program means or
the order in which a reader meets its parts (D-413).

This restates §4's rule 9 as a rule rather than a summary line, and
replaces §3.10's narrower "does not reorder params across groups", which
read as a grouping detail rather than a safety property. The stakes differ
by construct —
reordering a function's parameter list silently breaks every positional call
site, whereas reordering script `param` declarations (which are passed by
name, never positionally) would "only" churn diffs, `--help` ordering and
`.grobparams` template output. Both are unacceptable, and a formatter able
to change program meaning is not a formatter.

-----

## 4. What the Formatter Does Not Do

> **Meta-rule:** The formatter normalises whitespace, bracket placement,
> line breaks, and trailing commas. It does not change what the code
> means, says, or does in any other way.

Every item below follows from the meta-rule. They are listed explicitly
because users will ask.

1. **Does not reorder declarations.** Not types-before-functions, not
   alphabetical, not by visibility.
2. **Does not rename identifiers.** Not snake_case to camelCase. The
   naming idiom is taught through samples and documentation, not
   enforced by tooling (D-283).
3. **Does not insert or remove `const` or `readonly`.** Whether a binding
   is compile-time constant, runtime-once, or mutable is a semantic choice
   the author makes.
4. **Does not change import aliases.** Explicit or default, an alias is
   user intent.
5. **Does not normalise string literal escape sequences.** `"line\nbreak"`
   stays as written.
6. **Does not split or merge statements.** A statement on its own line
   stays on its own line.
7. **Does not refactor expressions.** `!(a == b)` is not simplified to
   `a != b`. `x == true` is not simplified to `x`.
8. **Does not convert between string literal forms.** `"..."`,
   `` `...` ``, and `` ```...``` `` remain as written, including for
   Windows paths.
9. **Does not reorder parameters or fields.** Extension of rule 1 for
   declarations. Stated in full, with the differing stakes per construct,
   in §3.14.
10. **Does not wrap long expressions.** Only bracketed constructs wrap.
    Long binary expressions, long strings, long identifiers stay long.
11. **Does not collapse or expand method chains based on anything other
    than length.** The §3.9 receiver-and-chain rule applies only when
    the single-line form exceeds 100 columns.
12. **Does not normalise numeric literal forms.** `0xFF` stays `0xFF`;
    `1_000_000` stays `1_000_000`.
13. **Does not normalise boolean or nil literals.** `true`/`false`/`nil`
    are the only forms; no substitutions.
14. **Does not modify block comment structure.** Internal indentation
    and closing `*/` placement are preserved.
15. **Does not add or remove parentheses.** Redundant parens stay;
    omitted parens stay omitted.
16. **Does not insert or remove trailing semicolons.** Grob has no
    semicolons.
17. **Does not lint or report warnings.** Unused variables, shadowing,
    and style concerns are compiler territory.
18. **Does not enforce the snake_case idiom.** Naming is documentation,
    not enforcement (D-283).
19. **Does not reformat generated code markers.** Future codegen markers
    are preserved verbatim.
20. **Does not interact with anything outside the file being formatted.**
    No `grob.json` consultation, no plugin resolution, no
    cross-file effects.

-----

## 5. Error Handling and Exit Codes

### Parse errors

The formatter runs the lexer and parser only. It does not run the type
checker.

On parse error, the formatter exits non-zero and writes the error to
stderr in the standard compiler diagnostic format. The file is not
modified. The formatter does not attempt partial formatting on
unparseable input.

### Success

On successful format, the formatter writes the formatted content to the
file in place and exits 0. No stdout output. No success message. Quiet
on success (D-063).

A file that is already in canonical form formats successfully with no
user-visible change.

### Exit codes

| Code | Meaning |
|---|---|
| 0 | Success — file was formatted (or was already in canonical form) |
| 1 | Parse error in input file |
| 2 | File I/O error (not found, permission denied, etc.) |
| 3 | Usage error (invalid CLI arguments) |
| 4 | `--check` mode: file is not in canonical form |

### Modes

**Default — format in place:**

```
grob fmt <file>
```

Formats the file in place. Exit 0 on success, 1–3 on error.

**Check — CI-friendly verification:**

```
grob fmt --check <file>
```

Does not modify the file. Exit 0 if the file is in canonical form; exit
4 if the file would be modified. On exit 4, a brief message is written
to stderr (`<file>: not formatted`). No diff is emitted; `git diff`
after running `grob fmt` shows the changes.

**Stdin — editor integration:**

```
grob fmt -
```

Reads from stdin, writes formatted output to stdout, does not modify any
file. Parse errors go to stderr.

### Scope for v1

One file per invocation. Directory recursion is post-MVP. Shell globbing
(`grob fmt *.grob` via a shell loop) is the interim pattern.

### Design constraint: idempotence

The formatter is idempotent: `fmt(fmt(x)) == fmt(x)` for all valid `x`.
Every rule in §3 is consistent with this property. The property is
testable — every formatted fixture in the test suite should re-format to
itself with zero diff.

-----

## 6. Worked Example

Input (poorly formatted, but parseable):

```grob
import   Grob.Http
import Grob.Crypto


import Company.Azure.Sdk

@secure
param token :string
param repo : string
param days_old :int=30

type Issue{
number:int
title: string
  created_at: string
    state:string
user :IssueUser
}

type IssueUser{
login :string
}

cutoff:=date.today().addDays(-days_old)

issues := http.get("https://api.github.com/repos/${repo}/issues?state=open",auth.bearer(token)).asJson().mapAs<Issue>()

print(issues.filter(i=>date.parse(i.created_at)<cutoff).select(i=>#{number:i.number,title:i.title,age:date.parse(i.created_at).daysUntil(date.today()),author:i.user.login}).formatAs.table())
```

Output (canonical):

```grob
import Grob.Http
import Grob.Crypto

import Company.Azure.Sdk

@secure
param token: string

param repo:     string
param days_old: int    = 30

type Issue {
    number:     int
    title:      string
    created_at: string
    state:      string
    user:       IssueUser
}

type IssueUser {
    login: string
}

cutoff := date.today().addDays(-days_old)

issues := http.get("https://api.github.com/repos/${repo}/issues?state=open", auth.bearer(token))
    .asJson()
    .mapAs<Issue>()

print(
    issues
        .filter(i => date.parse(i.created_at) < cutoff)
        .select(i => #{
            number: i.number,
            title:  i.title,
            age:    date.parse(i.created_at).daysUntil(date.today()),
            author: i.user.login,
        })
        .formatAs.table(),
)
```

What the formatter did:

- **Imports** (§3.13): stripped double blank lines within and between
  groups, normalised to single blank line between the two groups,
  normalised spacing around the `import` keyword.
- **Param declarations** (§3.10): the blank line below `@secure param
  token` inserted, isolating the decorated declaration in a formatting
  group of its own (D-413); `repo` and `days_old` left as the author wrote
  them, one group; `:` and `=` aligned within that group, `=` at the column
  set by `string`, the longest type in it. Both declarations remain in one
  parameter group for §19's ordering rule — the inserted blank line does
  not end it.
- **Type declaration** (§3.8): indented one level, `:` aligned across
  fields.
- **Spacing** (§3.6): normalised around `:=`, `:`, `,`, `=>`, `<`, `>`.
- **Method chain** (§3.9): receiver construction `http.get(...)` on
  first line, chain calls each on their own lines with leading dot.
- **Struct literal inside lambda** (§3.8): multi-line form, `:` aligned,
  trailing commas.
- **Outer `print(...)` call** (§3.2): wrapped because the single-line
  form exceeded 100 columns.

-----

## 7. Implementation Notes

- **AST-level, not token-level.** Formatter rules require semantic
  context that only parsing provides. Token-level formatting cannot
  distinguish unary minus from binary subtraction, postfix `++` from two
  unary expressions, namespace calls from method calls.
- **Shared with compiler.** The formatter shares its lexer, parser, and
  `DiagnosticBag` with `Grob.Compiler`. Parse errors produce the same
  diagnostic from `grob run` and `grob fmt`.
- **Single file per invocation for v1.** Directory recursion deferred
  post-MVP.
- **Idempotence is tested, not assumed.** The test suite includes a
  fixture for every rule in §3. Each fixture is formatted twice and
  verified to produce identical output.
- **Alignment scopes are independent, and are not always brace pairs.**
  The formatter does not perform cross-declaration or cross-block
  alignment. For braced constructs the scope is the brace pair; for
  `param` declarations, which have no braces since D-410, it is the §3.10
  formatting group. An implementer scoping param alignment to a brace will
  find none.

-----

## 8. Opinionated Defaults — Update

The Opinionated Defaults table in `grob-personality-identity.md` updates
to reflect the formatter's opinions:

| Area | Default opinion | Enforcement |
|---|---|---|
| Naming | `snake_case` idiomatic — see style guide | None (formatter, not compiler) |
| Nil handling | Explicit — no zero values, no silent nil | Compiler error |
| Unused variables | Worth knowing about | Compiler warning |
| Unused imports | Worth knowing about | Compiler warning |
| Line length | 100 columns | `grob fmt` wraps at 100 |
| Struct / type alignment | `:` aligned in multi-line declarations | `grob fmt` rewrites to aligned form |
| Formatting | `grob fmt` formats your code | Never automatic, always opt-in |

The "Line length: No opinion" row is replaced. Struct/type alignment is
a new row.

-----

*Updated August 2026 — D-421. §3.5's construct enumeration is completed and is*
*now declared complete, closing register item R-09. It previously named six*
*Category B constructs and omitted five comma-separated ones — switch-expression*
*arms, lambda parameter lists, function-type parameter lists, type-argument*
*lists and `select` case pattern lists — so the formatter had no rule for*
*constructs its own categorisation did not mention. Switch-expression arms join*
*Category B and are added to §3.2's wrappable list, which §3.1 had already*
*treated them as being. A new **Category C** covers the four comma-separated*
*constructs that are not wrappable under §3.2 and therefore never take a*
*trailing comma from the formatter, even though the grammar permits one. `for*
*k, v in` is named explicitly as a fixed pair rather than a list, so the*
*enumeration has no silent omissions. Category B gains an explicit statement*
*that a call with one wrapped argument is still a multi-line argument list and*
*takes the trailing comma — the reading §6's worked example already assumed and*
*no rule stated. Category A's "`param` blocks" is corrected to "`param`*
*declarations": D-414's terminology sweep recorded §3.11 as the last surviving*
*site of the form D-410 retired, and this one, in the section D-417 made*
*normative, was missed. No rule changes beyond the completed enumeration.*

-----

*Updated August 2026 — D-413. §3.10's decorated/undecorated alignment-group*
*binary is retired. Grouping is now the author's, delimited by blank lines*
*exactly as §3.3 already delimits import groups: the formatter preserves*
*those lines, collapses runs to one, and never merges groups. The one*
*separation the formatter owns is a blank line above and below a decorated*
*declaration, inserted if absent and not removable, so a decorator's scope*
*cannot be misread as reaching the declaration below. Alignment is computed*
*per group, which reinforces the author's grouping rather than flattening it.*
*The previous rule both forbade the blank lines a reader would add to break*
*up a long parameter list and, in its own worked example, ran two decorated*
*declarations adjacent with the stacks interleaved between aligned lines.*
*New §3.14 states the whole-formatter prohibition on reordering declarations*
*and parameters, previously implied only by a narrow "does not reorder across*
*groups" clause in §3.10. §7's alignment summary item 2 updated to match, and*
*"param blocks" retired as terminology throughout per D-410 — including*
*§3.11's function-signature alignment rule, the last site still carrying it.*
*§3.3 and §3.10 now say "formatting group" and state explicitly that it is*
*not §19's contiguity-delimited parameter group, so the blank lines this*
*specification requires are never read as ending a parameter group for the*
*E2202 ordering rule. §6's worked example was already correct as output;*
*its commentary bullet, which still described the retired binary, is*
*re-authored in the new terms.*

*Updated August 2026 — D-414 terminology and scope corrections; no rule*
*changes. §3.8's alignment-scope paragraph and §7's implementation note both*
*scoped alignment to "each brace pair", which was correct while `param { }`*
*existed and became wrong when D-410 retired it — `param` declarations have no*
*brace, so as written they had no alignment scope at all, six lines below the*
*list item that describes their alignment. Both now name the §3.10 formatting*
*group as the param scope and keep the brace pair for braced constructs.*
*§3.8's "In multi-line form" framing reworded for the same reason. §3.14's*
*claim that the reordering prohibition is "stated once" was false in the*
*document making it — §4's rule 9 already stated it — so §3.14 is reframed as*
*the full statement and rule 9 now points at it.*

*Formatter specification — Session D Part 2, April 2026.*
*Authored as language design partner and spec author.*
*Implementation reference for Sprint 12 of the v1 Requirements build plan.*
*April 2026 (Session B Interlude) — blank-line rule and non-feature wording*
*extended to cover `readonly` alongside `const` following D-288. `const` and*
*`readonly` form separate same-kind blocks for the alignment rules.*
