# Error-examples quarantine

Every pair in this directory either matches its gold master or has a row here (D-428
Decision 4). The harness runs a quarantined pair like any other and requires it to fail
exactly as its *Observed* cell records. A pair that starts matching its master fails the
harness until its row is removed. A pair that fails differently fails it with the recorded
list against the actual one. The negative-test release gate is this table empty.

The static checks on a master — titles, snippets, carets and locations — are never
quarantined. A row covers only the behavioural comparison.

**Columns.**

- *Pair* — the pair's directory name, in backticks.
- *Category* — `unbuilt` (the surface the pair exercises does not exist yet), `gap` (the
  compiler or runtime lacks something the master shows), `divergent` (the master and the
  compiler disagree and a decision must say which is right) or `stale` (the source predates
  the grammar or semantics it exercises).
- *Blocked by* — one or more of: an open row of the Deferred Work Register in
  `docs/design/grob-open-questions.md` (`R-26`), a namespace the stdlib catalogue does not
  register (`` `fs` ``) or a named sprint (`Sprint 11`), separated by `, `. A closed register
  row or a registered namespace fails the harness.
- *Reason* — one sentence on what the pair is waiting for, as measured.
- *Observed* — the ordered diagnostics the CLI writes for the pair today, each
  `severity[code] line:column` (`line` alone when the CLI gives no column), separated by
  `; `. A file is written before the line only when it is not the pair's own `.grob`. A pair
  that produces no diagnostics records `none`.

| Pair | Category | Blocked by | Reason | Observed |
|---|---|---|---|---|
| `catch-after-catch-all` | unbuilt | `fs` | `fs` is not registered, so `fs.delete` raises E1001 at 4:5 ahead of the master's E2205. | error[E1001] 4:5; error[E2205] 7:3 |
| `file-not-found` | unbuilt | `fs` | `fs` is not registered, so `fs.read` raises E1001 where the master's runtime E5301 belongs; once `fs` lands the runtime error meets R-26's missing column. | error[E1001] 2:13 |
| `file-not-found-via-stdlib` | unbuilt | `fs` | `fs` is not registered, so `fs.readLines` raises E1001 where the master's runtime E5301 belongs; once `fs` lands the runtime error meets R-26's missing column. | error[E1001] 3:10 |
| `json-parse-error` | unbuilt | `fs`, `json` | Neither `fs` nor `json` is registered, so `fs.read` and `json.parse` each raise E1001 in place of the master's runtime E5501; once both land the runtime error meets R-26's missing column. | error[E1001] 3:8; error[E1001] 4:8 |
| `permission-denied-in-nested-call` | unbuilt | `fs` | `fs` is not registered, so `fs.copy` raises E1001 where the master's runtime E5302 belongs; once `fs` lands the runtime error meets R-26's missing column. | error[E1001] 4:5 |
| `try-without-catch-or-finally` | unbuilt | `fs` | `fs` is not registered, so `fs.read` raises E1001 at 4:16 alongside the master's E2204. | error[E1001] 4:16; error[E2204] 3:5 |
| `process-exit-nonzero` | unbuilt | `process` | `process` is not registered, so `process.runOrFail` raises E1001 where the master's runtime E5602 belongs; once `process` lands the runtime error meets R-26's missing column. | error[E1001] 3:11 |
| `ambiguous-type-reference` | unbuilt | Sprint 11 | Plugin types arrive in Sprint 11; today `r: Response` resolves silently and the return raises E0005 at 6:12 in place of E3101 at 5:14. | error[E0005] 6:12 |
| `manifest-version-mismatch` | unbuilt | Sprint 11 | Plugin imports and their manifest checks arrive in Sprint 11; today the `import` raises nothing and `http.get` raises E1001 at 5:1 in place of E3201 at 3:1. | error[E1001] 5:1 |
| `plugin-not-installed` | unbuilt | Sprint 11 | Plugin imports arrive in Sprint 11; today the `import` raises nothing and `sharepoint.connect` raises E1001 at 5:1 in place of E3002 at 3:8. | error[E1001] 5:1 |
| `unknown-plugin` | unbuilt | Sprint 11 | Plugin imports arrive in Sprint 11; today the `import` raises nothing and `foo.greet` raises E1001 at 4:1 in place of E3001 at 2:8. | error[E1001] 4:1 |
| `array-index-out-of-range` | gap | R-26 | The runtime reports E5101 at line 4 with no column, where the master gives 4:7. | error[E5101] 4 |
| `array-index-out-of-range-in-function` | gap | R-26 | The runtime reports E5101 at line 4 with no column, where the master gives 4:12. | error[E5101] 4 |
| `call-stack-overflow` | gap | R-26 | The runtime reports E5901 at line 4 with no column, where the master gives 4:12. | error[E5901] 4 |
| `circular-initialisation` | gap | R-26 | The runtime reports E5902 at line 5 with no column, where the master gives 5:12. | error[E5902] 5 |
| `integer-division-by-zero` | gap | R-26 | The runtime reports E5002 at line 5 with no column, where the master gives 5:10. | error[E5002] 5 |
| `math-domain-violation` | gap | R-26 | The runtime reports E5006 at line 3 with no column, where the master gives 3:7. | error[E5006] 3 |
| `required-env-var-not-set` | gap | R-26 | The runtime reports E5801 at line 3 with no column, where the master gives 3:10. | error[E5801] 3 |
| `forward-reference-in-function` | divergent | R-27 | E1201 has no throw site; the checker raises E1001 at the master's location. | error[E1001] 4:15 |
| `invalid-implicit-conversion-float-to-int` | divergent | R-27 | E0007 has no throw site; the checker raises E0001 at the master's location. | error[E0001] 4:20 |
| `nil-dereference-at-runtime` | divergent | R-27 | The dereference is now rejected statically as E0101 at 4:14, followed by a cascading E0005 at 5:12, so the master's runtime E5201 is never reached. | error[E0101] 4:14; error[E0005] 5:12 |
| `shadowed-declaration-loop-var` | divergent | R-27 | E1101 has no throw site; the pair compiles and runs with no diagnostic. | none |
| `type-mismatch-bool-arithmetic` | divergent | R-27 | The checker raises E0002 for `bool + int` at the master's location, where the master states E0001. | error[E0002] 3:12 |
| `undefined-method-on-int` | divergent | R-27 | E0006 has no throw site; the checker raises E1002 at the master's location. | error[E1002] 4:7 |
| `use-before-declaration-in-block` | divergent | R-27 | E1202 has no throw site; the checker raises E1001 at the master's location. | error[E1001] 6:27 |
| `nullable-interpolation` | divergent | R-28 | The checker points at `${` (4:20), the master at the interpolated expression (4:22). | error[E0102] 4:20 |
| `type-cycle-no-terminating-field` | divergent | R-28 | The checker reports the cycle at its first field (4:5), the master at the field that closes it (8:5). | error[E0301] 4:5 |
| `unterminated-string-literal` | divergent | R-28 | The lexer reports the column after the opening quote (3:13), the master the quote itself (3:12). | error[E2002] 3:13 |
| `unexpected-token-missing-brace` | divergent | R-29 | A missing `{` after the signature yields five diagnostics where the master states one E2001 at 4:5. | error[E2014] 5:1; error[E2001] 3:31; error[E2001] 5:1; error[E2203] 4:5; error[E1001] 4:22 |
