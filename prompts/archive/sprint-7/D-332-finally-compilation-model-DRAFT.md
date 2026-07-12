# D-332 (DRAFT) — `finally` compilation model: non-exceptional paths

> **Provisional number.** The tail of the live decisions log at the time this was
> drafted was **D-331**. Confirm the real next-free number against the live log tail
> before committing — never take the number from this draft (the D-306 collision and
> the D-331 stale-zip lessons). This entry is the record of the one load-bearing
> Sprint 7 decision, agreed at the Sprint 7 kickoff and implemented in Increment C.
> Insert all three pieces below in lockstep: the summary index row, the full entry,
> and the footer changelog line.

---

## 1 — Summary index row (insert in the summary table, in number order)

| D-332 | June 2026 | Exception handling — finally / compiler | `finally` compilation model for the non-exceptional paths. D-275 fixed the exceptional path (handler-table `finallyOffset`, VM runs the finally before propagating); the non-exceptional exits — normal try/catch fall-through and early `return`/`break`/`continue` — are **compiled, not VM-intercepted**. The closed `OpCode` enum (§3.3) carries no `Leave`/`EndFinally` and none is added. The compiler tracks the stack of enclosing `try`/`finally` regions and, at every control-transfer site that leaves one or more of them, emits each pending finally body inline innermost-to-outermost, each exactly once. The E2207 control-flow-in-`finally` ban keeps every emitted chain straight-line and terminating; the one non-straight-line finally exit is `throw`, which re-enters the unwinding path and replaces the in-flight exception (D-275). `exit()`/`ExitSignal` (D-110) unwinds past every finally without running any — a VM-level teardown, not a chained transfer. The javac-post-`jsr` duplication model: a finally body may appear at several sites. No `Leave`/`EndFinally` opcode, no enum growth (ADR-0013 untouched), no new error code. Extends D-275. Sprint 7 Increment C |

---

## 2 — Full entry (insert in number order among the `### D-###` entries)

### D-332 — `finally` compilation model: non-exceptional paths (June 2026)

Area: Exception handling — finally / compiler
Supersedes: none (extends D-275)
Superseded by: none

D-275 fixed the **exceptional** `finally` path: the exception handler table gains a
`finallyOffset` per entry and the VM runs the finally block before propagating. What
D-275 left open — and this entry decides — is the **non-exceptional** paths: normal
fall-through completion of a try or catch body, and early `return`/`break`/`continue`
leaving one or more enclosing `try`/`finally` regions.

The closed `OpCode` enum (§3.3, ADR-0013) carries `TryBegin`/`TryEnd`/`Throw` but no
`Leave`/`EndFinally`, so the VM cannot intercept a `Return` or a `Jump` to run a
pending finally. Rather than grow the enum, the non-exceptional paths are **compiled**.
The compiler tracks the stack of enclosing `try`/`finally` regions and, at every
control-transfer site that leaves one or more of them, emits each pending finally
block's body inline, **innermost-to-outermost**, before the transfer instruction.
Normal completion emits the finally once at the fall-through. A non-local exit through
*N* nested `try`/`finally` regions emits *N* finally bodies, each **exactly once**, in
order. This includes `break`/`continue` whose target loop sits outside one or more
intervening `try`/`finally` regions — the intervening finallys are emitted before the
loop-control transfer.

The partition is exact: **compiler-emitted** finally on every straight-line
fall-through and early-exit path; **VM-run** finally (`finallyOffset`) on every
exceptional propagation path (uncaught from a try, thrown from a catch). A finally
body's bytecode may therefore appear at several sites — the classic javac-post-`jsr`
duplication — which is expected and correct.

The E2207 control-flow-in-`finally` ban (D-275) is what makes the emitted chain
analyzable: `return`/`break`/`continue` inside a finally body are compile errors, so a
finally body never itself transfers control and every emitted chain is straight-line
and terminating. The single finally-body exit that is not straight-line is a `throw`,
which is permitted, re-enters the unwinding path, and replaces the in-flight exception
(D-275; no chaining in v1). The D-276 carve-out holds: a `return` inside a block-body
lambda nested in a finally exits only the lambda and is not E2207.

`exit()`/`ExitSignal` (D-110, D-274) is not a chained transfer — it unwinds past every
finally without running any, a VM-level stack teardown the compiler does not chain and
the VM does not run `finallyOffset` for.

Rationale: a dedicated `Leave`/`EndFinally` opcode would grow a wire-format surface
(ADR-0013, the `.grobc` format) for a mechanism the compiler handles cleanly, and
`finally`'s own control-flow ban makes the duplication bounded and analyzable.
Duplication trades a modest bytecode-size increase for a closed enum and a simpler VM —
the right trade for a scripting VM where the finally bodies are small and the enum's
stability contract is load-bearing. No `Leave`/`EndFinally` opcode, no enum growth, no
new error code; count unchanged. Sprint 7 Increment C.

---

## 3 — Footer changelog line (append to the footer changelog for this session)

- **D-332** — `finally` compilation model for the non-exceptional paths: compiler-
  emitted finally chains (innermost-to-outermost, each exactly once) rather than a new
  `Leave`/`EndFinally` opcode; VM-run `finallyOffset` retained for the exceptional path
  (extends D-275). No enum growth, no new error code.
