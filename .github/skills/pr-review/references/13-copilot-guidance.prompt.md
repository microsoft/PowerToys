# Step 13: Copilot Guidance Review (Conditional)

**Goal**: Review changes to Copilot/AI guidance files and ensure code changes align with existing guidance.

## When to run this step
- Run **only if** the PR contains changes to:
  - `*copilot*.md` files
  - `.github/prompts/*.md` files
  - `.github/copilot-instructions.md`
  - `.github/instructions/*.md` files
  - `**/SKILL.md` files
  - Agent/prompt configuration files

## Output file
`Generated Files/prReview/{{pr_number}}/13-copilot-guidance.md`

## Checks to execute

### Guidance file quality
- [ ] Is the prompt clear and unambiguous?
- [ ] Are instructions actionable and specific?
- [ ] Is the scope well-defined?
- [ ] Are examples provided where helpful?
- [ ] Is the guidance consistent with repo conventions?

### Prompt engineering best practices
- [ ] Is the goal stated clearly at the beginning?
- [ ] Are constraints and boundaries specified?
- [ ] Are output formats defined?
- [ ] Are edge cases addressed?
- [ ] Is the prompt tested with sample inputs?

### Consistency checks
- [ ] Is terminology consistent with other guidance files?
- [ ] Are file paths and references correct?
- [ ] Do applyTo patterns match intended files?
- [ ] Are referenced tools/scripts available?

### SKILL.md structure (if applicable)
- [ ] Is the skill name and description clear?
- [ ] Are prerequisites documented?
- [ ] Are usage examples provided?
- [ ] Is the expected output described?
- [ ] Are references properly linked?

### Code alignment with guidance
- [ ] Do code changes follow existing Copilot instructions?
- [ ] Are new patterns documented in guidance?
- [ ] Do changes require guidance updates?
- [ ] Are breaking changes to AI workflows documented?

## PowerToys-specific guidance checks
- [ ] Is guidance aligned with AGENTS.md?
- [ ] Are component-specific instructions referenced?
- [ ] Is build guidance accurate?
- [ ] Are test expectations documented?
- [ ] Is the guidance discoverable (proper location)?

## Common issues to flag
```markdown
# BAD: Vague instruction
Make sure the code is good.

# GOOD: Specific instruction
Ensure all public methods have XML documentation comments
including <summary>, <param>, and <returns> tags.

# BAD: Missing context
Use the Logger class.

# GOOD: With context
Use the Logger class from `src/common/logger/` for all logging.
Follow the patterns in doc/devdocs/development/logging.md.

# BAD: Hardcoded paths
Edit the file at C:\Users\dev\PowerToys\src\...

# GOOD: Relative/generic paths
Edit the file at src/modules/<module>/...
```

## File template
```md
# Copilot Guidance Review
**PR:** {{pr_number}} — Base:{{baseRefName}} Head:{{headRefName}}
**Review iteration:** {{iteration}}

## Iteration history
### Iteration {{iteration}}
- <Key finding 1>
- <Key finding 2>

## Guidance files changed
| File | Change type | Assessment |
|------|-------------|------------|
| path/to/file.md | Added/Modified | ✅/⚠️/❌ |

## Checks executed
- <List specific guidance checks performed>

## Findings
```mcp-review-comment
{"file":"path/to/guidance.md","start_line":10,"end_line":15,"severity":"high|medium|low|info","tags":["copilot-guidance","pr-{{pr_number}}"],"body":"Guidance issue → Impact on AI workflows → Suggested improvement."}
```
```

## Severity guidelines
- **High**: Incorrect guidance leading to wrong AI behavior, broken workflows
- **Medium**: Unclear instructions, missing important context
- **Low**: Minor clarity improvements, formatting
- **Info**: Enhancement suggestions for better AI assistance

## External references (MUST research)
Before completing this step, **fetch and analyze** these authoritative sources:

| Reference | URL | Check for |
| --- | --- | --- |
| Agent Skills Spec | https://agentskills.io/ | Skill format compliance |
| VS Code Custom Instructions | https://code.visualstudio.com/docs/copilot/customization | Instruction patterns |
| GitHub Copilot Extensions | https://docs.github.com/en/copilot/customizing-copilot | Customization best practices |
| Prompt Engineering | https://platform.openai.com/docs/guides/prompt-engineering | Prompt writing patterns |

**Enforcement**: Include `## References consulted` section with guidance standards checked.

## Related files
- `.github/copilot-instructions.md` - Main Copilot guidance
- `AGENTS.md` - AI contributor guide
- `.github/instructions/*.md` - Component-specific instructions
- `.github/prompts/*.md` - Task-specific prompts
