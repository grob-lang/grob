# Snippets for files not in the uploaded zip

The zip contained `.claude/` only, so these three edits could not be made
directly. Each is ready to paste.

---

## 1. Root `CLAUDE.md` — degradation self-check (change 9)

A skill cannot fire on "I feel foggy" — skills trigger on task shape, not on
internal state. This has to live in the always-loaded layer. Place it near the
existing session-conduct rules.

```markdown
## Session degradation

Watch for these while you work:

- Re-reading a file already read this session.
- Restating a constraint already established.
- Asking a question already answered.
- Drifting outside the increment's stated scope.
- Crossing the plan-mode or read-only gate without an explicit approval turn.

Any one alone is ordinary. Two together mean your grip on your own context has
slipped, and continuing costs more than stopping. Say so plainly and offer
`/handoff`. Do not push on, and do not wait to be asked.
```

Cost: roughly 90 tokens per turn. That is the price of the check being available
at the moment it is needed rather than at the moment someone thinks to invoke it.

---

## 2. `.gitignore` (change 11)

```gitignore
# Session scratch — hand-offs, investigation reports
.grob-session/
```

---

## 3. `.pre-commit-config.yaml` — prose check (change 7)

Local hook, warn-only, staged markdown only. `always_run: false` with the `files`
filter means a code-only commit never invokes it.

```yaml
  - repo: local
    hooks:
      - id: prose-check
        name: house-style prose check (warn only)
        entry: pwsh -NoProfile -File tooling/prose-check.ps1
        language: system
        files: \.md$
        pass_filenames: false
        verbose: true
```

`verbose: true` matters — without it, `pre-commit` suppresses stdout on a passing
hook and the warnings are never seen. The script always exits 0 by design; see
its header for why that must not change.
