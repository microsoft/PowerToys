# Phase 7-8: Summary Generation and Final Release Notes

## Phase 7: Generate Summary Markdown (Agent Mode)

For each CSV in `Generated Files/ReleaseNotes/grouped_csv/`, create a markdown file in `Generated Files/ReleaseNotes/grouped_md/`.

⚠️ **IMPORTANT:** Generate **ALL** markdown files first. Do NOT pause between files or ask for feedback during generation. Complete the entire batch, then human reviews afterwards.

### Structure per file

**1. Bullet list** - one concise, user-facing line per PR:
- Use "Verbed + Scenario + Impact" sentence structure—readers should feel "wow, that's what I need" or "yes, that's an awesome fix"
- If nothing special or unclear, mark as needing human summary
- Source from Title, Body, and CopilotSummary (prefer CopilotSummary when available)
- If the column `NeedThanks` in CSV is `True`, append: `Thanks [@Author](https://github.com/Author)!`
- Do NOT include PR numbers in bullet lines
- Do NOT mention "security fix" directly—describe user-facing scenario impact instead
- If confidence < 70%, write: `Human Summary Needed: <PR full link>`

**See [SampleOutput.md](./SampleOutput.md) for examples of well-written bullet summaries.**

**2. Three-column table** (same PR order):
- Column 1: Concise summary (same as bullet)
- Column 2: PR link `[#ID](URL)`
- Column 3: Confidence level (High/Medium/Low)

### Review Process (AFTER all files generated)

- Human reviews each `grouped_md/*.md` file and requests rewrites as needed
- Human may say "rewrite Product-X" or "combine these bullets"—apply changes to that specific file
- Do NOT interrupt generation to ask for feedback

---

## Phase 8: Produce Final Release Notes File

Once all `grouped_md/*.md` files are reviewed and approved, consolidate into a single release notes file.

**Output:** `Generated Files/ReleaseNotes/v{{ReleaseVersion}}-release-notes.md`

### Structure

**1. Highlights section** (top):
- 8-12 bullets covering the most user-visible features and impactful fixes
- Pattern: `**Module**: brief description`
- Avoid internal refactors; focus on what users will notice

**2. Module sections** (alphabetical order):
- One section per product (Advanced Paste, Awake, Command Palette, etc.)
- Copy bullet summaries from the approved `grouped_md/Product-*.md` files
- Include `Thanks @contributor!` attributions inline

### Example Final Structure

```markdown
# PowerToys v{{ReleaseVersion}} Release Notes

## Highlights

- **Command Palette**: Added theme customization and drag-and-drop support
- **Advanced Paste**: Image input for AI, color detection in clipboard history
- **FancyZones**: New CLI tool for command-line layout management
...

---

## Advanced Paste

- Wrapped paste option lists in a single ScrollViewer
- Added image input handling for AI-powered transformations
...

## Awake

- Fixed timed mode expiration. Thanks [@daverayment](https://github.com/daverayment)!
...

---

## Contributors

- [@artickc](https://github.com/artickc)
- [@daverayment](https://github.com/daverayment)
...
```
