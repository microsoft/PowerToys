---
description: 'Guidelines for creating high-quality prompt files for GitHub Copilot'
applyTo: '**/*.prompt.md'
---

# Copilot Prompt Files Guidelines

Instructions for creating effective and maintainable prompt files that guide GitHub Copilot in delivering consistent, high-quality outcomes across any repository.

## Scope and Principles
- Target audience: maintainers and contributors authoring reusable prompts for Copilot Chat.
- Goals: predictable behaviour, clear expectations, minimal permissions, and portability across repositories.
- Primary references: VS Code documentation on prompt files and organization-specific conventions.

## Frontmatter Requirements

Every prompt file should include YAML frontmatter with the following fields:

### Required/Recommended Fields

| Field | Required | Description |
|-------|----------|-------------|
| `description` | Recommended | A short description of the prompt (single sentence, actionable outcome) |
| `name` | Optional | The name shown after typing `/` in chat. Defaults to filename if not specified |
| `agent` | Recommended | The agent to use: `ask`, `edit`, `agent`, or a custom agent name. Defaults to current agent |
| `model` | Optional | The language model to use. Defaults to currently selected model |
| `tools` | Optional | List of tool/tool set names available for this prompt |
| `argument-hint` | Optional | Hint text shown in chat input to guide user interaction |

### Guidelines

- Use consistent quoting (single quotes recommended) and keep one field per line for readability and version control clarity
- If `tools` are specified and current agent is `ask` or `edit`, the default agent becomes `agent`
- Preserve any additional metadata (`language`, `tags`, `visibility`, etc.) required by your organization

## File Naming and Placement
- Use kebab-case filenames ending with `.prompt.md` and store them under `.github/prompts/` unless your workspace standard specifies another directory.
- Provide a short filename that communicates the action (for example, `generate-readme.prompt.md` rather than `prompt1.prompt.md`).

## Body Structure
- Start with an `#` level heading that matches the prompt intent so it surfaces well in Quick Pick search.
- Organize content with predictable sections. Recommended baseline: `Mission` or `Primary Directive`, `Scope & Preconditions`, `Inputs`, `Workflow` (step-by-step), `Output Expectations`, and `Quality Assurance`.
- Adjust section names to fit the domain, but retain the logical flow: why → context → inputs → actions → outputs → validation.
- Reference related prompts or instruction files using relative links to aid discoverability.

## Input and Context Handling
- Use `${input:variableName[:placeholder]}` for required values and explain when the user must supply them. Provide defaults or alternatives where possible.
- Call out contextual variables such as `${selection}`, `${file}`, `${workspaceFolder}` only when they are essential, and describe how Copilot should interpret them.
- Document how to proceed when mandatory context is missing (for example, “Request the file path and stop if it remains undefined”).

## Tool and Permission Guidance
- Limit `tools` to the smallest set that enables the task. List them in the preferred execution order when the sequence matters.
- If the prompt inherits tools from a chat mode, mention that relationship and state any critical tool behaviours or side effects.
- Warn about destructive operations (file creation, edits, terminal commands) and include guard rails or confirmation steps in the workflow.

## Instruction Tone and Style
- Write in direct, imperative sentences targeted at Copilot (for example, “Analyze”, “Generate”, “Summarize”).
- Keep sentences short and unambiguous, following Google Developer Documentation translation best practices to support localization.
- Avoid idioms, humor, or culturally specific references; favor neutral, inclusive language.

## Output Definition
- Specify the format, structure, and location of expected results (for example, “Create an architecture decision record file using the template below, such as `docs/architecture-decisions/record-XXXX.md`).
- Include success criteria and failure triggers so Copilot knows when to halt or retry.
- Provide validation steps—manual checks, automated commands, or acceptance criteria lists—that reviewers can execute after running the prompt.

## Examples and Reusable Assets
- Embed Good/Bad examples or scaffolds (Markdown templates, JSON stubs) that the prompt should produce or follow.
- Maintain reference tables (capabilities, status codes, role descriptions) inline to keep the prompt self-contained. Update these tables when upstream resources change.
- Link to authoritative documentation instead of duplicating lengthy guidance.

## Quality Assurance Checklist
- [ ] Frontmatter fields are complete, accurate, and least-privilege.
- [ ] Inputs include placeholders, default behaviours, and fallbacks.
- [ ] Workflow covers preparation, execution, and post-processing without gaps.
- [ ] Output expectations include formatting and storage details.
- [ ] Validation steps are actionable (commands, diff checks, review prompts).
- [ ] Security, compliance, and privacy policies referenced by the prompt are current.
- [ ] Prompt executes successfully in VS Code (`Chat: Run Prompt`) using representative scenarios.

## Maintenance Guidance
- Version-control prompts alongside the code they affect; update them when dependencies, tooling, or review processes change.
- Review prompts periodically to ensure tool lists, model requirements, and linked documents remain valid.
- Coordinate with other repositories: when a prompt proves broadly useful, extract common guidance into instruction files or shared prompt packs.

## Additional Resources
- [Prompt Files Documentation](https://code.visualstudio.com/docs/copilot/customization/prompt-files#_prompt-file-format)
- [Awesome Copilot Prompt Files](https://github.com/github/awesome-copilot/tree/main/prompts)
- [Tool Configuration](https://code.visualstudio.com/docs/copilot/chat/chat-agent-mode#_agent-mode-tools)
