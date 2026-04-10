# Commit Skill

## Step 1 — Gather full context (run all in parallel)

- `git status --short` — identify every changed/untracked file
- `git diff HEAD` — full diff of all tracked changes (staged + unstaged)
- `git log --oneline -5` — recent commits to match message style
- For every **untracked** file shown by `git status`, read its full content with the Read tool — `git diff HEAD` does NOT show new files

## Step 2 — Write the commit message

Based on the above (not on prior conversation context), write a concise message:
- Follow the style of recent commits in the log
- Describe the feature/fix, not just "update X"
- One sentence is enough; add a blank line + bullet details only if genuinely complex

## Step 3 — Stage and commit

1. `git add -A` — stage ALL files (assets, prefabs, meta files included; per CLAUDE.md)
2. `git commit -m "<message>"` using a HEREDOC to preserve formatting
3. `git log --oneline -1` — confirm the commit landed
4. `git push` — push to remote
