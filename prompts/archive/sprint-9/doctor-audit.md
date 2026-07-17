One security note before the report: while scanning transcripts for denied commands, I found a historical Bash(export SONAR_TOKEN=...) call with a live-looking token value in plaintext (from an automode-blocked denial). That's outside /doctor's scope to fix, but worth flagging directly — if that token is still valid, consider rotating it on SonarCloud. I won't repeat the value here.

Doctor report
Your setup is largely healthy — one npm-global install, no broken settings, all 7 subagents load cleanly, no Claude Code hooks configured anywhere, and no local (CLAUDE.local.md/~/.claude/CLAUDE.md) files to dedupe. The real findings are: Claude Code is a few releases behind, auto mode isn't your default permission mode, and your checked-in CLAUDE.md files have a handful of small, safe trims (none currently near the 40k-char warning size). Scan window: skillUsage lifetime is 2026-06-17 → 2026-07-16 (whole project history); transcripts cover the 50 most recent sessions (2026-06-25 → 2026-07-16) out of 73 total.

Component Type Scope Uses (total since install) Used in window? Est. resident tokens Verdict
adding-a-stdlib-function skill project 0 no ~43 keep — 0 uses to date, but Sprint 9-B onward (date/fs/json/csv/process/regex) is exactly what this skill is for
adding-an-opcode skill project 0 no ~44 keep — root CLAUDE.md names this skill directly as the sanctioned opcode-growth procedure; removing it breaks that pointer
allocating-an-error-code skill project 4 no ~142 keep — real use
authoring-a-plugin skill project 0 no ~43 keep — plugins/CLAUDE.md names it directly; Sprint 9's capability-interface work (fs/process) will use it
defining-a-type skill project 0 no ~156 keep — 0 uses to date, but Sprint 9-B (date) is the first type-carrying module this skill exists for
emitting-closures-and-upvalues skill project 1 no ~95 keep — used once (Sprint 5-D), thin recent signal is expected, not disuse
extending-the-grammar skill project 1 no ~124 keep
grob-spec-lookup skill project 1 no ~85 keep
logging-a-decision skill project 14 yes ~42 keep — heavily used
sonar-pr-review (+ grob: form) skill project 18 yes ~132 keep — heavily used
tdd-cycle (+ grob: form) skill project 18 yes ~63 keep — heavily used
trunk-flow skill project 2 no ~69 keep
writing-an-error-test skill project 6 no ~45 keep
writing-grob-source (+ grob: form) skill project 2 yes ~103 keep
microsoft-docs (project .mcp.json) MCP server project n/a (no counter) no (0 tool calls in window) deferred keep — grob-compiler-engineer and grob-design-reviewer subagents grant this exact tool name; disabling breaks their tool access, not a cost issue
claude.ai Microsoft Learn connector MCP server account n/a (no counter) yes (3 calls) deferred keep — real use, distinct consumer (main session, not subagents)
claude.ai Context7 connector MCP server account n/a (no counter) no deferred no local disable mechanism — see note below
claude.ai Mermaid Chart connector MCP server account n/a (no counter) no deferred no local disable mechanism
claude.ai Gmail connector MCP server account n/a (no counter) no deferred never authorized either
claude.ai Google Calendar connector MCP server account n/a (no counter) no deferred never authorized either
claude.ai Google Drive connector MCP server account n/a (no counter) no deferred never authorized either
Plugins — — none installed — — nothing to report
grob/CLAUDE.md (root) CLAUDE.md always-loaded — — ~1,970 2 small cuts proposed below
grob/src/CLAUDE.md CLAUDE.md nested (loads in src/) — — ~5,975 1 dedup proposed below; largest file, still well under the ~40k-char warning floor
grob/tests/CLAUDE.md CLAUDE.md nested (loads in tests/) — — ~1,278 already lean — nothing to cut
grob/plugins/CLAUDE.md CLAUDE.md nested (loads in plugins/) — — ~822 already lean — nothing to cut
On the claude.ai connectors with no local disable mechanism: Context7, Mermaid Chart, Gmail, Calendar and Drive cost nothing in context (deferred) and none fired in the 50-session window — Gmail/Calendar/Drive were never even authorized. Decluttering, not saving tokens, would be the only reason to drop them, and that's done via claude.ai's own connector settings, not a file I can edit here — flagging for your awareness, not proposing a local action.

Proposed actions
Check 3 — trim derivable content

grob/CLAUDE.md: delete the "Repo harness map (D-314)" section (9 lines) — it's a repo tour (CLAUDE.md, .claude/commands/, .claude/agents/, prompts/, docs/design/) reconstructable by ls and reading a couple of files. Est. save ~280 tokens/turn (root loads every session).
grob/src/CLAUDE.md: the file carries two overlapping assembly-ownership tables — "Where code belongs (the DAG)" and "What goes where" — describing the same project/type mapping twice. Consolidate into the one DAG table (which also carries the "May reference" column the second table lacks) and drop "What goes where". Est. save ~150 tokens when working in src/.
Check 4 — migrate to lazy loading / fix drift found along the way 3. grob/CLAUDE.md: the "Decisions-log entries follow ADR style..., updated in four-location lockstep... summary index row, full entry, status table, footer changelog" line duplicates the logging-a-decision skill's own "keep three things in lockstep" section — except the skill's checklist only lists three (full entry, summary index, footer changelog) and is missing the "status table" step CLAUDE.md claims. Trim CLAUDE.md to a one-line pointer at the skill, and add the missing conditional bullet ("project-status table, where applicable") to the skill's checklist so the detail has one correct home instead of two disagreeing ones.

Check 7 — version 4. Installed 2.1.181, latest on the latest channel (unset channel defaults there) is 2.1.211 — autoUpdates is on and nothing disables it, so this looks like it just hasn't caught up yet. Propose running claude update.

Housekeeping noticed along the way (not a named check, but concrete and safe) 5. grob/.claude/settings.local.json lists "github" in enabledMcpjsonServers, but no github server exists in .mcp.json — a harmless dangling entry. Propose removing it. 6. The outer .claude/settings.local.json (d:/Code/grob-lang/.claude/settings.local.json) has one allow rule, Bash(python -c ' \*), that's already granted by the checked-in .claude/settings.json at the same level — pure duplicate. Propose removing it.

Warnings (no action)
Check 5 (slow hooks): none configured in any settings scope (user/project/local), and none fired in the 50-session window. Nothing to warn about.
Check 6 (context-heavy extensions): nothing alarming. Largest always-resident item is root CLAUDE.md (~1,970 tokens); the project skill listing totals ~1,187 tokens against the ~1% skill-listing budget, no truncation risk. No non-deferred MCP tool schemas are resident. /context will give you the exact live figure to compare against these disk-based estimates.
Two entries for this project exist in ~/.claude.json's projects map (D:/Code/grob-lang/grob and D:\Code\grob-lang\grob, forward vs backslash) — harmless path-casing artifact, not something to hand-edit.
Everything above is safe and reversible: 4 skills are recommended to stay despite zero recent use (roadmap-relevant, or directly named by CLAUDE.md), 2 small CLAUDE.md trims plus 1 drift fix, an update, and 2 tiny duplicate/dangling settings entries. Nothing here changes engineering behaviour, quality gates or the design corpus. Clean up everything?
