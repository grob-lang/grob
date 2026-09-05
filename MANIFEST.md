# Harness change manifest — September 2026

A zip cannot express a deletion, so the removals below must be done by hand. The
zip is otherwise a drop-in over `.claude/`, `tooling/` and `prompts/`.

## Delete

All ten increment prompts from `.claude/commands/`. They live in `prompts/` only
from now on, per the amended rule now recorded in `trunk-flow`.

```text
.claude/commands/sprint-9-a.md
.claude/commands/sprint-9-a2.md
.claude/commands/sprint-9-a3.md
.claude/commands/sprint-9-b.md
.claude/commands/sprint-9-c.md
.claude/commands/sprint-9-d.md
.claude/commands/sprint-9-e.md
.claude/commands/sprint-9-f.md
.claude/commands/sprint-9-g.md
.claude/commands/sprint-9-h.md
```

**Verify before deleting.** The July audit confirmed these are byte-identical
mirrors of their `prompts/archive/sprint-9/` counterparts, but it verified two of
ten by sample. Run the full comparison first — if any file has diverged, the
`.claude/` copy holds content the archive does not, and deleting it loses it:

```powershell
Get-ChildItem .claude/commands/sprint-9-*.md | ForEach-Object {
    $archive = "prompts/archive/sprint-9/$($_.Name)"
    if (-not (Test-Path $archive)) { "MISSING ARCHIVE: $($_.Name)"; return }
    $mine = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    $theirs = (Get-FileHash $archive -Algorithm SHA256).Hash
    if ($mine -ne $theirs) { "DIVERGED: $($_.Name)" } else { "identical: $($_.Name)" }
}
```

Anything reported as `DIVERGED` or `MISSING ARCHIVE` needs reconciling before the
delete, not after.

## Move

Four Opus specialist agents, all pinned to closed sprints — Sprint 4 (lowering),
5 (closure), 7 (unwind), 8 (namespace dispatch). None will be invoked again, and
their descriptions cost roughly 597 tokens every turn. Kept rather than deleted
because they are the template for the next carve-out; `grob-namespace-dispatch`
is the best-written of the four and the one to read first.

```text
.claude/agents/grob-closure-specialist.md            -> prompts/archive/agents/
.claude/agents/grob-lowering-specialist.md           -> prompts/archive/agents/
.claude/agents/grob-unwind-specialist.md             -> prompts/archive/agents/
.claude/agents/grob-namespace-dispatch-specialist.md -> prompts/archive/agents/
```

The zip contains them at the destination path, so this is a delete from
`.claude/agents/` once the zip is unpacked.

## New

```text
.claude/skills/house-style/SKILL.md
.claude/commands/handoff.md
tooling/prose-check.ps1
```

## Modified

```text
.claude/skills/trunk-flow/SKILL.md              increment-prompt location rule; style pointer
.claude/skills/logging-a-decision/SKILL.md      house-style checklist item
.claude/skills/writing-grob-source/SKILL.md     style pointer
.claude/skills/writing-an-error-test/SKILL.md   style pointer (two sites)
.claude/skills/adding-a-stdlib-function/SKILL.md   style pointer
.claude/skills/authoring-a-plugin/SKILL.md      style pointer
.claude/commands/commit-message.md              prose pass section; style pointer
.claude/agents/grob-compiler-engineer.md        style pointer
.claude/agents/grob-reviewer.md                 style pointer
```

## By hand

See `snippets/APPLY-THESE.md` — root `CLAUDE.md`, `.gitignore` and
`.pre-commit-config.yaml` were not in the uploaded zip.

## Verified

`tooling/prose-check.ps1` was authored in a container with no PowerShell and so
shipped statically reviewed only. It has since been executed, and the dry run
earned its place — it found three defects that static review had missed:

- The `pre-commit` entry needed `-ExecutionPolicy Bypass`. The script is unsigned
  and the local execution policy blocked it.
- The emoji rule used JS/ICU `\u{1F300}-\u{1FAFF}` syntax, which .NET's regex
  engine rejects. Replaced with the surrogate-pair range
  `[\uD800-\uDBFF][\uDC00-\uDFFF]`.
- The diff parser discarded added lines whose own text began with `++`, treating
  them as a `+++` file header. That dropped the line from the scan and left
  `$lineNo` short for the rest of the hunk, misreporting every later match.

The regex behaviour was also checked against the false positive the July audit
named — a two-clause `decisions, and the format matters` does not match, and a
genuine three-item list does.

Repeat the dry run after any change to the script, before trusting the hook:

```powershell
pwsh tooling/prose-check.ps1 -Path docs/design/grob-decisions-log.md
```

Expect noise on that file. It is the largest prose surface in the corpus and the
one that has never had the rule stated on its authoring path.
