# Grob — Error Examples Library

This directory contains worked examples for every error code emitted by the
Grob compiler and runtime. Each example is a minimal Grob script plus the
exact diagnostic that script produces. Together they form the gold-master
fixtures for the diagnostic-emission test harness, the source material for
the documentation site, and a reference for anyone — human or AI — adding
new error codes or new examples.

The authoritative registry of error codes lives at `grob-error-codes.md`.
The rendering format is fixed by `grob-v1-requirements.md` §10. The voice
constraints are fixed by D-059, D-064, D-077, and `grob-personality-identity.md`.

-----

## Layout

Each example is a directory whose name is a kebab-case slug describing the
failure mode:

```
docs/errors/examples/
├── README.md
├── array-index-out-of-range/
│   ├── array-index-out-of-range.grob
│   └── array-index-out-of-range.expected.txt
├── const-reassignment/
│   ├── const-reassignment.grob
│   └── const-reassignment.expected.txt
└── ...
```

The slug describes the scenario, not the code. The error code is in the
header comment of the `.grob` file and in the `error[Exxxx]:` line of the
`.expected.txt`. Where multiple examples for one code emerge later, the
slug disambiguates by scenario.

-----

## File layout in project knowledge vs on disk

Until the Grob source repository is live, this project lives entirely in
Claude project knowledge — a flat namespace with no directory support.
The canonical on-disk layout (the `<slug>/<slug>.grob` directory pairs
shown above) is reconstructed at the point a real repo is created.

While the project is online-only, the example files in project knowledge
use the flat naming convention:

```
example-<slug>.grob
example-<slug>.expected.txt
errors-examples-README.md
```

The mapping to the canonical layout is mechanical:

| In project knowledge          | On disk                                            |
|-------------------------------|----------------------------------------------------|
| `example-<slug>.grob`         | `docs/errors/examples/<slug>/<slug>.grob`          |
| `example-<slug>.expected.txt` | `docs/errors/examples/<slug>/<slug>.expected.txt`  |
| `errors-examples-README.md`   | `docs/errors/examples/README.md`                   |

Later batches uploaded into project knowledge follow the same
convention. When development starts and the repo is created, a one-shot
script splits the `example-` prefix off the filename and reassembles
the directory layout. The on-disk layout above is the canonical
reference for the test harness and `grob --explain Exxxx`.

-----

## File pairs

Every example is exactly two files:

**`<slug>.grob`** — a minimal, self-contained Grob script that produces
exactly one diagnostic. Nothing in the script that does not contribute to
producing the error. The first line is a single `//` comment naming the
code and the intent:

```grob
// E0001 — adding an int to a bool, with no obvious fix
```

**`<slug>.expected.txt`** — the byte-exact stderr output produced by a
correct v1 Grob implementation when given the `.grob` file. Plain text,
no ANSI colour codes, single trailing newline (POSIX). The path in the
`-->` line is the bare script filename — no directory prefix.

The `.expected.txt` is the gold master. If a test fixture's actual output
drifts from this file by a single byte, the test fails.

-----

## Running the examples

```
grob check <slug>.grob   # compile-time examples (E0xxx through E4xxx)
grob run   <slug>.grob   # runtime examples (E5xxx)
```

`grob check` runs the parser and type checker without executing. `grob run`
runs the script. Either command writes the diagnostic to stderr and exits
with a non-zero status.

To regenerate an `.expected.txt` from a known-good build:

```
grob check <slug>.grob 2> <slug>.expected.txt
```

Always inspect the diff before committing — unintended changes in the
diagnostic surface are exactly what these fixtures are designed to catch.

-----

## Relationship to `grob --explain`

`grob --explain Exxxx` reads the long-form per-code documentation at
`docs/errors/Exxxx.md`. That document has four sections: cause, example,
fix, see also. The "example" section quotes from this directory. The
two surfaces are deliberately separate:

- **Registry** (`grob-error-codes.md`) — one-sentence description per code,
  status, source decision.
- **Examples library** (this directory) — minimal reproducer plus expected
  output, gold-master for the test harness.
- **Per-code docs** (`docs/errors/Exxxx.md`) — narrative cause, fix and cross-references for `grob --explain`.

A new error code requires an entry in all three.

-----

## The diagnostic format

The compiler and runtime render errors in a single uniform format, fixed by
`grob-v1-requirements.md` §10:

```
error[Exxxx]: <title>
  --> <file>:<line>:<col>
   |
 N | <source line>
   | <caret highlight>
   |
   = note: <fact>
   = help: <suggested fix>
```

Conventions locked by Session D Part 3 and applied to every example here:

- **Title case:** lowercase, matching rustc convention. `error[E0001]: type
  mismatch`, not `error[E0001]: Type Mismatch`.
- **Note prefixes:** `= note:` for facts about what was expected, what was
  got, and what the type checker inferred. `= help:` for actionable
  suggestions. Bare `= ` for continuation lines that complete a previous
  note (rare).
- **No colour:** `.expected.txt` is plain text. ANSI colour is an opt-in
  interactive render mode, not part of the gold master.
- **One error per example:** multi-error rendering is a Sprint 2 test
  harness concern. Each example pair documents a single error.
- **Names and types, never values** (D-077): the diagnostic shows
  identifiers and types. Values render only where they are intrinsic to
  the failure (an array length, an out-of-range index, a literal path) and
  cannot be a credential. A `--verbose` mode overrides this for debugging.
- **`help:` only when the fix is obvious** (D-059): if there is no obvious
  fix, the diagnostic stops at notes. Guessing at a fix is worse than
  silence.
- **No "simply", no emoji** (D-064).

-----

## Runtime diagnostics

Runtime errors render in the same `error[Exxxx]:` format as compile-time
errors. The `Exxxx` code carries the phase. The throw site is rendered as
`-->  <file>:<line>:<col>`, identical to a compile-time location. The
thrown leaf type (`IoError`, `IndexError`, etc.) is identified via the
registry; the diagnostic surface does not duplicate it.

Multi-frame stack rendering for unhandled exceptions that propagate
through user functions is a separate render decision and is not exercised
by the examples in this initial batch — every runtime example here throws
at top level. The chain render lands in a later batch with a dedicated
format note.

-----

## Stack-frame chain rendering

Format locked in Session D Part 3 Continuation (Batch 3). Applies to
runtime examples where an exception propagates through one or more
user-authored function frames before reaching top level.

1. **`at` vs `from` separator.** User-authored function frames use `at`.
   Stdlib frames use `from`. The distinction signals whether the call site
   in the chain is in user code or inside a stdlib operation.

2. **Chain order.** The throw site appears first (innermost), followed by
   each calling frame in outward order. The final frame is always
   `at <top-level>`.

3. **Top-level frame is `<top-level>`.** Rejected `<script>` (too vague)
   and omitting the frame (makes the chain feel truncated).

4. **Stdlib-internal frames are not shown.** The user-visible call site is
   the actionable location. The `from` prefix on the user-visible call site
   is sufficient to communicate that the throw originated inside stdlib code.

5. **Single-frame throws omit the chain block entirely.** When the throw is
   directly at top level, the `-->` line is the full picture. The chain
   block is conditional on there being at least one non-top-level frame.

-----

## Warning-severity rendering

Format locked in Session D Part 3 Continuation (Batch 3). Applies to
examples where the compiler emits a `warning[Exxxx]:` diagnostic rather
than a fatal `error[Exxxx]:`.

1. **Severity prefix is `warning[Exxxx]:`** — same `Exxxx` code as the
   registry entry. No separate `Wxxxx` namespace. Consistent with
   ADR-0017's choice to keep warnings in the `Exxxx` space so a future
   `--strict` mode can promote them without renumbering.

2. **Exit code for warning-only output is 0.** The script compiled and
   ran. A non-zero exit code would break pipelines that should succeed.
   The warning is on stderr.

3. **Gold-master files do not encode colour.** The `.expected.txt` files
   are plain text. Warm-amber warning colour is an interactive render
   concern, not a gold-master concern.

-----

## Adding a new example

1. Read the relevant entry in `grob-error-codes.md` — the one-sentence
   description is the constraint.
2. Read the source decision (D-### or LF section) listed under the
   registry entry. The example must be faithful to the decision, not a
   plausible-sounding scenario.
3. Pick a kebab-case slug describing the failure. If the code already has
   an example, the slug should disambiguate by scenario, not by ordinal.
4. Write the minimal `.grob` script. Strip everything that does not
   contribute to producing the error.
5. Write the `.expected.txt` by hand, byte-exact, single trailing
   newline. Do not use placeholders for line numbers or paths.
6. Re-read the voice constraints above before finalising the wording of
   any title, note or help line.

-----

## Code → slug index

| Code | Slug | Category |
|------|------|----------|
| E0001 | `type-mismatch-bool-arithmetic` | Type |
| E0002 | `incompatible-operands-string-comparison` | Type |
| E0003 | `wrong-argument-count-too-few` | Type |
| E0004 | `argument-type-mismatch-string-for-int` | Type |
| E0005 | `return-type-mismatch-string-for-int` | Type |
| E0006 | `undefined-method-on-int` | Type |
| E0007 | `invalid-implicit-conversion-float-to-int` | Type |
| E0008 | `named-argument-before-positional` | Type |
| E0009 | `named-argument-names-required-parameter` | Type |
| E0010 | `duplicate-named-argument` | Type |
| E0011 | `unknown-parameter-name` | Type |
| E0101 | `nil-deref-method-call` | Type |
| E0102 | `nullable-interpolation` | Type |
| E0201 | `const-reassignment` | Type |
| E0202 | `readonly-reassignment` | Type |
| E0301 | `type-cycle-no-terminating-field` | Type |
| E1001 | `undefined-identifier-typo` | Name resolution |
| E1002 | `undefined-member-access` | Name resolution |
| E1101 | `shadowed-declaration-loop-var` | Name resolution |
| E1201 | `forward-reference-in-function` | Name resolution |
| E1202 | `use-before-declaration-in-block` | Name resolution |
| E2001 | `unexpected-token-missing-brace` | Syntax |
| E2002 | `unterminated-string-literal` | Syntax |
| E2005 | `invalid-escape-sequence` | Syntax |
| E2201 | `import-after-declaration` | Syntax |
| E2203 | `top-level-return` | Syntax |
| E2204 | `try-without-catch-or-finally` | Syntax |
| E2205 | `catch-after-catch-all` | Syntax |
| E2211 | `break-inside-select` | Syntax |
| E2212 | `break-continue-outside-loop` | Syntax |
| E3001 | `unknown-plugin` | Module |
| E3002 | `plugin-not-installed` | Module |
| E3101 | `ambiguous-type-reference` | Module |
| E3201 | `manifest-version-mismatch` | Module |
| E4001 | `unknown-decorator` | Param / decorator |
| E4002 | `decorator-not-permitted-here` | Param / decorator |
| E4101 | `invalid-allowed-argument` | Param / decorator |
| E4202 | `param-after-param-block-ends` | Param / decorator |
| E5002 | `integer-division-by-zero` | Runtime |
| E5101 | `array-index-out-of-range` | Runtime |
| E5101 | `array-index-out-of-range-in-function` | Runtime |
| E5201 | `nil-dereference-at-runtime` | Runtime |
| E5301 | `file-not-found` | Runtime |
| E5301 | `file-not-found-via-stdlib` | Runtime |
| E5302 | `permission-denied-in-nested-call` | Runtime |
| E5501 | `json-parse-error` | Runtime |
| E5602 | `process-exit-nonzero` | Runtime |
| E5801 | `required-env-var-not-set` | Runtime |
| E5901 | `call-stack-overflow` | Runtime |
| E5902 | `circular-initialisation` | Runtime |

-----

*Initial batch: 12 examples, Session D Part 3, April 2026.*
*Format conventions and voice locked by this session — see*
*`session-D-part-3-summary.md` for rationale on each decision.*

*Batch 2: 24 examples, Session D Part 3 Continuation, April 2026.*
*Batch 3: 4 examples (3 stack-frame chain, 1 warning), Session D Part 3 Continuation, April 2026.*
*Stack-frame chain render and warning-severity render formats locked in Batch 3.*

*Sprint 5 Increment B: 4 examples (E0008–E0011, the named-argument call-site*
*diagnostics), June 2026. Source decision D-318 (D-113).*
