agent: 'agent'
model: GPT-5.1-Codex-Max
description: 'Generate an 80-character git commit title for the local diff.'
---

**Goal:** Provide a ready-to-paste git commit title (<= 80 characters) that captures the most important local changes since `HEAD`.

**Workflow:**
1. Run a single command to view the local diff since the last commit:
   ```@terminal
   git diff HEAD
   ```
2. From that diff, identify the dominant area (reference key paths like `src/modules/*`, `doc/devdocs/**`, etc.), the type of change (bug fix, docs update, config tweak), and any notable impact.
3. Draft a concise, imperative commit title summarizing the dominant change. Keep it plain ASCII, <= 80 characters, and avoid trailing punctuation. Mention the primary component when obvious (for example `FancyZones:` or `Docs:`).
4. Respond with only the final commit title on a single line so it can be pasted directly into `git commit`.
