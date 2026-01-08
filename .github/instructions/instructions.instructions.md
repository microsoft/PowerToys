---
description: 'Guidelines for creating high-quality custom instruction files for GitHub Copilot'
applyTo: '**/*.instructions.md'
---

# Custom Instructions File Guidelines

Instructions for creating effective and maintainable custom instruction files that guide GitHub Copilot in generating domain-specific code and following project conventions.

## Project Context

- Target audience: Developers and GitHub Copilot working with domain-specific code
- File format: Markdown with YAML frontmatter
- File naming convention: lowercase with hyphens (e.g., `react-best-practices.instructions.md`)
- Location: `.github/instructions/` directory
- Purpose: Provide context-aware guidance for code generation, review, and documentation

## Required Frontmatter

Every instruction file must include YAML frontmatter with the following fields:

```yaml
---
description: 'Brief description of the instruction purpose and scope'
applyTo: 'glob pattern for target files (e.g., **/*.ts, **/*.py)'
---
```

### Frontmatter Guidelines

- **description**: Single-quoted string, 1-500 characters, clearly stating the purpose
- **applyTo**: Glob pattern(s) specifying which files these instructions apply to
  - Single pattern: `'**/*.ts'`
  - Multiple patterns: `'**/*.ts, **/*.tsx, **/*.js'`
  - Specific files: `'src/**/*.py'`
  - All files: `'**'`

## File Structure

A well-structured instruction file should include the following sections:

### 1. Title and Overview

- Clear, descriptive title using `#` heading
- Brief introduction explaining the purpose and scope
- Optional: Project context section with key technologies and versions

### 2. Core Sections

Organize content into logical sections based on the domain:

- **General Instructions**: High-level guidelines and principles
- **Best Practices**: Recommended patterns and approaches
- **Code Standards**: Naming conventions, formatting, style rules
- **Architecture/Structure**: Project organization and design patterns
- **Common Patterns**: Frequently used implementations
- **Security**: Security considerations (if applicable)
- **Performance**: Optimization guidelines (if applicable)
- **Testing**: Testing standards and approaches (if applicable)

### 3. Examples and Code Snippets

Provide concrete examples with clear labels:

```markdown
### Good Example
\`\`\`language
// Recommended approach
code example here
\`\`\`

### Bad Example
\`\`\`language
// Avoid this pattern
code example here
\`\`\`
```

### 4. Validation and Verification (Optional but Recommended)

- Build commands to verify code
- Linting and formatting tools
- Testing requirements
- Verification steps

## Content Guidelines

### Writing Style

- Use clear, concise language
- Write in imperative mood ("Use", "Implement", "Avoid")
- Be specific and actionable
- Avoid ambiguous terms like "should", "might", "possibly"
- Use bullet points and lists for readability
- Keep sections focused and scannable

### Best Practices

- **Be Specific**: Provide concrete examples rather than abstract concepts
- **Show Why**: Explain the reasoning behind recommendations when it adds value
- **Use Tables**: For comparing options, listing rules, or showing patterns
- **Include Examples**: Real code snippets are more effective than descriptions
- **Stay Current**: Reference current versions and best practices
- **Link Resources**: Include official documentation and authoritative sources

### Common Patterns to Include

1. **Naming Conventions**: How to name variables, functions, classes, files
2. **Code Organization**: File structure, module organization, import order
3. **Error Handling**: Preferred error handling patterns
4. **Dependencies**: How to manage and document dependencies
5. **Comments and Documentation**: When and how to document code
6. **Version Information**: Target language/framework versions

## Patterns to Follow

### Bullet Points and Lists

```markdown
## Security Best Practices

- Always validate user input before processing
- Use parameterized queries to prevent SQL injection
- Store secrets in environment variables, never in code
- Implement proper authentication and authorization
- Enable HTTPS for all production endpoints
```

### Tables for Structured Information

```markdown
## Common Issues

| Issue            | Solution            | Example                       |
| ---------------- | ------------------- | ----------------------------- |
| Magic numbers    | Use named constants | `const MAX_RETRIES = 3`       |
| Deep nesting     | Extract functions   | Refactor nested if statements |
| Hardcoded values | Use configuration   | Store API URLs in config      |
```

### Code Comparison

```markdown
### Good Example - Using TypeScript interfaces
\`\`\`typescript
interface User {
  id: string;
  name: string;
  email: string;
}

function getUser(id: string): User {
  // Implementation
}
\`\`\`

### Bad Example - Using any type
\`\`\`typescript
function getUser(id: any): any {
  // Loses type safety
}
\`\`\`
```

### Conditional Guidance

```markdown
## Framework Selection

- **For small projects**: Use Minimal API approach
- **For large projects**: Use controller-based architecture with clear separation
- **For microservices**: Consider domain-driven design patterns
```

## Patterns to Avoid

- **Overly verbose explanations**: Keep it concise and scannable
- **Outdated information**: Always reference current versions and practices
- **Ambiguous guidelines**: Be specific about what to do or avoid
- **Missing examples**: Abstract rules without concrete code examples
- **Contradictory advice**: Ensure consistency throughout the file
- **Copy-paste from documentation**: Add value by distilling and contextualizing

## Testing Your Instructions

Before finalizing instruction files:

1. **Test with Copilot**: Try the instructions with actual prompts in VS Code
2. **Verify Examples**: Ensure code examples are correct and run without errors
3. **Check Glob Patterns**: Confirm `applyTo` patterns match intended files

## Example Structure

Here's a minimal example structure for a new instruction file:

```markdown
---
description: 'Brief description of purpose'
applyTo: '**/*.ext'
---

# Technology Name Development

Brief introduction and context.

## General Instructions

- High-level guideline 1
- High-level guideline 2

## Best Practices

- Specific practice 1
- Specific practice 2

## Code Standards

### Naming Conventions
- Rule 1
- Rule 2

### File Organization
- Structure 1
- Structure 2

## Common Patterns

### Pattern 1
Description and example

\`\`\`language
code example
\`\`\`

### Pattern 2
Description and example

## Validation

- Build command: `command to verify`
- Linting: `command to lint`
- Testing: `command to test`
```

## Maintenance

- Review instructions when dependencies or frameworks are updated
- Update examples to reflect current best practices
- Remove outdated patterns or deprecated features
- Add new patterns as they emerge in the community
- Keep glob patterns accurate as project structure evolves

## Additional Resources

- [Custom Instructions Documentation](https://code.visualstudio.com/docs/copilot/customization/custom-instructions)
- [Awesome Copilot Instructions](https://github.com/github/awesome-copilot/tree/main/instructions)
