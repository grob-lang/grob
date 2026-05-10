---
mode: 'agent'
description: 'Generate a conventional-commits message for the currently staged changes. Inspect the staged diff, choose the right type and scope and produce a complete message body.'
---

# Generate commit message

Inspect the currently staged changes (`git diff --cached`) and produce a
commit message that follows the conventions in
`.github/instructions/commits.instructions.md`.

## Procedure

1. Run `git status` to confirm there are staged changes and identify which
   files are touched.
2. Run `git diff --cached` to see the actual changes.
3. Run `git branch --show-current` to confirm you are **not** on `main`.
   If you are on `main`, stop. Say "I'm on main — this commit must go on
   a branch. Should I create one?" and wait for instruction.
4. Identify the conventional-commits type:
   - New behaviour the user can observe → `feat`
   - Bug fix in existing behaviour → `fix`
   - Internal restructure with no behaviour change → `refactor`
   - Test-only changes → `test`
   - Docs, comments, spec files → `docs`
   - csproj, .slnx, NuGet → `build`
   - GitHub Actions workflows → `ci`
   - Genuinely none of the above → `chore`
5. Identify the scope from the files touched:
   - One project → that project's scope (`core`, `compiler`, `vm`,
     `runtime`, `stdlib`, `cli`, `lsp`)
   - Multiple test projects → `tests`
   - Multiple docs → `docs`
   - Build configuration → `build`
   - Genuinely multi-project → omit the scope (and consider whether this
     should be split)
6. Write the subject: imperative mood, lowercase first letter, no trailing
   full stop, ≤50 characters where possible.
7. Write the body: wrapped at 72 characters, explaining *why* the change
   was made. Reference the decision-log entry if applicable
   (`Implements D-###.`).
8. Add a footer only if there's an issue to close or a breaking change to
   flag.

## Split detection

After drafting, look at the staged changes again with fresh eyes. Ask:

- **Do all the changes serve one purpose?** A `feat` commit that also
  touches an unrelated comment in another file is two commits.
- **Could a reviewer revert this commit cleanly?** If reverting would
  remove two unrelated things, the commit is doing two things.
- **Are the type and scope honest?** A change tagged `refactor` that
  introduces a new public method isn't a refactor.

If the answer to any of these is "no", surface it before committing:

> "These staged changes look like two commits to me:
>
> 1. `feat(compiler): add lexer for raw string literals` (Lexer.cs,
>    TokenKind.cs, LexerTests.cs)
> 2. `docs(compiler): clarify rule for backtick escape sequences`
>    (grob-language-fundamentals.md)
>
> Should I split them, or commit them together with a multi-purpose
> message?"

## Output

Present the proposed message in a fenced block, ready to paste into
`git commit -m` or `git commit -F -`:

```
<type>(<scope>): <subject>

<body wrapped at 72 chars>

<footer if any>
```

Then ask: "Commit this, or revise?"

Wait for Chris's response. Do not run `git commit` autonomously.

## What this prompt does not do

- Does not stage anything. If there's nothing staged, say so.
- Does not commit. The commit is Chris's action.
- Does not push. Push happens after Chris's review.
- Does not merge. Merges are always Chris's action.
