---
description: 'Guidelines for creating high-quality Agent Skills for GitHub Copilot'
applyTo: '**/.github/skills/**/SKILL.md, **/.claude/skills/**/SKILL.md'
---

# Agent Skills File Guidelines

Instructions for creating effective and portable Agent Skills that enhance GitHub Copilot with specialized capabilities, workflows, and bundled resources.

## What Are Agent Skills?

Agent Skills are self-contained folders with instructions and bundled resources that teach AI agents specialized capabilities. Unlike custom instructions (which define coding standards), skills enable task-specific workflows that can include scripts, examples, templates, and reference data.

Key characteristics:
- **Portable**: Works across VS Code, Copilot CLI, and Copilot coding agent
- **Progressive loading**: Only loaded when relevant to the user's request
- **Resource-bundled**: Can include scripts, templates, examples alongside instructions
- **On-demand**: Activated automatically based on prompt relevance

## Directory Structure

Skills are stored in specific locations:

| Location | Scope | Recommendation |
|----------|-------|----------------|
| `.github/skills/<skill-name>/` | Project/repository | Recommended for project skills |
| `.claude/skills/<skill-name>/` | Project/repository | Legacy, for backward compatibility |
| `~/.github/skills/<skill-name>/` | Personal (user-wide) | Recommended for personal skills |
| `~/.claude/skills/<skill-name>/` | Personal (user-wide) | Legacy, for backward compatibility |

Each skill **must** have its own subdirectory containing at minimum a `SKILL.md` file.

## Required SKILL.md Format

### Frontmatter (Required)

```yaml
---
name: webapp-testing
description: Toolkit for testing local web applications using Playwright. Use when asked to verify frontend functionality, debug UI behavior, capture browser screenshots, check for visual regressions, or view browser console logs. Supports Chrome, Firefox, and WebKit browsers.
license: Complete terms in LICENSE.txt
---
```

| Field | Required | Constraints |
|-------|----------|-------------|
| `name` | Yes | Lowercase, hyphens for spaces, max 64 characters (e.g., `webapp-testing`) |
| `description` | Yes | Clear description of capabilities AND use cases, max 1024 characters |
| `license` | No | Reference to LICENSE.txt (e.g., `Complete terms in LICENSE.txt`) or SPDX identifier |

### Description Best Practices

**CRITICAL**: The `description` field is the PRIMARY mechanism for automatic skill discovery. Copilot reads ONLY the `name` and `description` to decide whether to load a skill. If your description is vague, the skill will never be activated.

**What to include in description:**
1. **WHAT** the skill does (capabilities)
2. **WHEN** to use it (specific triggers, scenarios, file types, or user requests)
3. **Keywords** that users might mention in their prompts

**Good description:**
```yaml
description: Toolkit for testing local web applications using Playwright. Use when asked to verify frontend functionality, debug UI behavior, capture browser screenshots, check for visual regressions, or view browser console logs. Supports Chrome, Firefox, and WebKit browsers.
```

**Poor description:**
```yaml
description: Web testing helpers
```

The poor description fails because:
- No specific triggers (when should Copilot load this?)
- No keywords (what user prompts would match?)
- No capabilities (what can it actually do?)

### Body Content

The body contains detailed instructions that Copilot loads AFTER the skill is activated. Recommended sections:

| Section | Purpose |
|---------|---------|
| `# Title` | Brief overview of what this skill enables |
| `## When to Use This Skill` | List of scenarios (reinforces description triggers) |
| `## Prerequisites` | Required tools, dependencies, environment setup |
| `## Step-by-Step Workflows` | Numbered steps for common tasks |
| `## Troubleshooting` | Common issues and solutions table |
| `## References` | Links to bundled docs or external resources |

## Bundling Resources

Skills can include additional files that Copilot accesses on-demand:

### Supported Resource Types

| Folder | Purpose | Loaded into Context? | Example Files |
|--------|---------|---------------------|---------------|
| `scripts/` | Executable automation that performs specific operations | When executed | `helper.py`, `validate.sh`, `build.ts` |
| `references/` | Documentation the AI agent reads to inform decisions | Yes, when referenced | `api_reference.md`, `schema.md`, `workflow_guide.md` |
| `assets/` | **Static files used AS-IS** in output (not modified by the AI agent) | No | `logo.png`, `brand-template.pptx`, `custom-font.ttf` |
| `templates/` | **Starter code/scaffolds that the AI agent MODIFIES** and builds upon | Yes, when referenced | `viewer.html` (insert algorithm), `hello-world/` (extend) |

### Directory Structure Example

```
.github/skills/my-skill/
├── SKILL.md              # Required: Main instructions
├── LICENSE.txt           # Recommended: License terms (Apache 2.0 typical)
├── scripts/              # Optional: Executable automation
│   ├── helper.py         # Python script
│   └── helper.ps1        # PowerShell script
├── references/           # Optional: Documentation loaded into context
│   ├── api_reference.md
│   ├── workflow-setup.md     # Detailed workflow (>5 steps)
│   └── workflow-deployment.md
├── assets/               # Optional: Static files used AS-IS in output
│   ├── baseline.png      # Reference image for comparison
│   └── report-template.html
└── templates/            # Optional: Starter code the AI agent modifies
    ├── scaffold.py       # Code scaffold the AI agent customizes
    └── config.template   # Config template the AI agent fills in
```

> **LICENSE.txt**: When creating a skill, download the Apache 2.0 license text from https://www.apache.org/licenses/LICENSE-2.0.txt and save as `LICENSE.txt`. Update the copyright year and owner in the appendix section.

### Assets vs Templates: Key Distinction

**Assets** are static resources **consumed unchanged** in the output:
- A `logo.png` that gets embedded into a generated document
- A `report-template.html` copied as output format
- A `custom-font.ttf` applied to text rendering

**Templates** are starter code/scaffolds that **the AI agent actively modifies**:
- A `scaffold.py` where the AI agent inserts logic
- A `config.template` where the AI agent fills in values based on user requirements
- A `hello-world/` project directory that the AI agent extends with new features

**Rule of thumb**: If the AI agent reads and builds upon the file content → `templates/`. If the file is used as-is in output → `assets/`.

### Referencing Resources in SKILL.md

Use relative paths to reference files within the skill directory:

```markdown
## Available Scripts

Run the [helper script](./scripts/helper.py) to automate common tasks.

See [API reference](./references/api_reference.md) for detailed documentation.

Use the [scaffold](./templates/scaffold.py) as a starting point.
```

## Progressive Loading Architecture

Skills use three-level loading for efficiency:

| Level | What Loads | When |
|-------|------------|------|
| 1. Discovery | `name` and `description` only | Always (lightweight metadata) |
| 2. Instructions | Full `SKILL.md` body | When request matches description |
| 3. Resources | Scripts, examples, docs | Only when Copilot references them |

This means:
- Install many skills without consuming context
- Only relevant content loads per task
- Resources don't load until explicitly needed

## Content Guidelines

### Writing Style

- Use imperative mood: "Run", "Create", "Configure" (not "You should run")
- Be specific and actionable
- Include exact commands with parameters
- Show expected outputs where helpful
- Keep sections focused and scannable

### Script Requirements

When including scripts, prefer cross-platform languages:

| Language | Use Case |
|----------|----------|
| Python | Complex automation, data processing |
| pwsh | PowerShell Core scripting |
| Node.js | JavaScript-based tooling |
| Bash/Shell | Simple automation tasks |

Best practices:
- Include help/usage documentation (`--help` flag)
- Handle errors gracefully with clear messages
- Avoid storing credentials or secrets
- Use relative paths where possible

### When to Bundle Scripts

Include scripts in your skill when:
- The same code would be rewritten repeatedly by the agent
- Deterministic reliability is critical (e.g., file manipulation, API calls)
- Complex logic benefits from being pre-tested rather than generated each time
- The operation has a self-contained purpose that can evolve independently
- Testability matters — scripts can be unit tested and validated
- Predictable behavior is preferred over dynamic generation

Scripts enable evolution: even simple operations benefit from being implemented as scripts when they may grow in complexity, need consistent behavior across invocations, or require future extensibility.

### Security Considerations

- Scripts rely on existing credential helpers (no credential storage)
- Include `--force` flags only for destructive operations
- Warn users before irreversible actions
- Document any network operations or external calls

## Common Patterns

### Parameter Table Pattern

Document parameters clearly:

```markdown
| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `--input` | Yes | - | Input file or URL to process |
| `--action` | Yes | - | Action to perform |
| `--verbose` | No | `false` | Enable verbose output |
```

## Validation Checklist

Before publishing a skill:

- [ ] `SKILL.md` has valid frontmatter with `name` and `description`
- [ ] `name` is lowercase with hyphens, ≤64 characters
- [ ] `description` clearly states **WHAT** it does, **WHEN** to use it, and relevant **KEYWORDS**
- [ ] Body includes when to use, prerequisites, and step-by-step workflows
- [ ] SKILL.md body kept under 500 lines (split large content into `references/` folder)
- [ ] Large workflows (>5 steps) split into `references/` folder with clear links from SKILL.md
- [ ] Scripts include help documentation and error handling
- [ ] Relative paths used for all resource references
- [ ] No hardcoded credentials or secrets

## Workflow Execution Pattern

When executing multi-step workflows, create a TODO list where each step references the relevant documentation:

```markdown
## TODO
- [ ] Step 1: Configure environment - see [workflow-setup.md](./references/workflow-setup.md#environment)
- [ ] Step 2: Build project - see [workflow-setup.md](./references/workflow-setup.md#build)
- [ ] Step 3: Deploy to staging - see [workflow-deployment.md](./references/workflow-deployment.md#staging)
- [ ] Step 4: Run validation - see [workflow-deployment.md](./references/workflow-deployment.md#validation)
- [ ] Step 5: Deploy to production - see [workflow-deployment.md](./references/workflow-deployment.md#production)
```

This ensures traceability and allows resuming workflows if interrupted.

## Related Resources

- [Agent Skills Specification](https://agentskills.io/)
- [VS Code Agent Skills Documentation](https://code.visualstudio.com/docs/copilot/customization/agent-skills)
- [Reference Skills Repository](https://github.com/anthropics/skills)
- [Awesome Copilot Skills](https://github.com/github/awesome-copilot/blob/main/docs/README.skills.md)
