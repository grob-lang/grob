---
name: "Start Sprint Task"
description: "Kick off a single scoped sprint task with the right agent, skill and acceptance criterion identified."
agent: "Grob Sprint Implementer"
tools: ["search", "read", "edit", "execute"]
---

# Start a Grob sprint task

You are starting work on one scoped task: **${input:task:Describe the task or paste the issue}**.

Work in this order — do not skip the grounding:

1. **Locate it.** Find the task in `docs/design/grob-v1-requirements.md` §4. State which
   sprint it belongs to and quote the exact acceptance criterion you are satisfying. If
   it is in the §13 out-of-scope list, say so and stop.
2. **Check the authority.** Search `docs/design/grob-decisions-log.md` for any `D-###`
   governing this area. The log wins over any other doc.
3. **Pick the right skill** if one applies, and follow it:
    - touching the opcode set → `adding-an-opcode`
    - adding to a core module → `adding-a-stdlib-function`
    - a plugin → `authoring-a-plugin`
    - an error case or diagnostic → `writing-an-error-test`
    - the decision needs logging → `logging-a-decision`
4. **Confirm the invariants in play.** Source location, `ResolvedType`/`Declaration`,
   all-errors-collected, error-recovering parser, typed opcodes, the Compiler/Vm DAG
   split — whichever the task touches.
5. **Implement the smallest working increment.** Stay strictly inside the task; a
   focused diff is a reviewable diff. Write the tests alongside.
6. **Verify.** Run `dotnet build` and `dotnet test`; report the result. Nothing is done
   until both are green.

If the task is ambiguous or appears to conflict with a decision, stop and ask with the
specific file and line — do not guess.

**Model suggestion:** for mechanical, well-specified implementation work, Sonnet is the
efficient choice; switch to a stronger model for tasks that turn on design judgement.
