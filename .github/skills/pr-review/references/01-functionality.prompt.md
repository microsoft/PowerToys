# Step 01: Functionality Review

**Goal**: Verify the PR's code changes correctly implement the intended functionality without introducing regressions.

## Output file
`Generated Files/prReview/{{pr_number}}/01-functionality.md`

## Checks to execute

### Core functionality
- [ ] Does the code do what the PR description/linked issue claims?
- [ ] Are all acceptance criteria from the linked issue addressed?
- [ ] Do new features work correctly in both enabled and disabled states?
- [ ] Are feature flags/settings properly respected?

### Logic correctness
- [ ] Are conditional branches handling all expected cases?
- [ ] Are loops terminating correctly (no infinite loops, off-by-one errors)?
- [ ] Are null/empty checks in place where needed?
- [ ] Are error conditions handled gracefully?
- [ ] Are edge cases considered (empty input, max values, boundary conditions)?

### State management
- [ ] Is state properly initialized before use?
- [ ] Is state cleaned up appropriately (disposal, event unsubscription)?
- [ ] Are race conditions possible with shared state?
- [ ] Is state persisted/loaded correctly for settings?

### Integration points
- [ ] Do changes integrate correctly with existing code paths?
- [ ] Are dependencies properly injected/resolved?
- [ ] Do IPC/interprocess communications work correctly?
- [ ] Are module enable/disable transitions handled?

### PowerToys-specific checks
- [ ] Does the module interface contract remain intact?
- [ ] Are hotkey registrations/unregistrations balanced?
- [ ] Does the feature work correctly with Runner lifecycle?
- [ ] Are Settings UI changes reflected in the module behavior?

## File template
```md
# Functionality Review
**PR:** {{pr_number}} — Base:{{baseRefName}} Head:{{headRefName}}
**Review iteration:** {{iteration}}

## Iteration history
### Iteration {{iteration}}
- <Key finding 1>
- <Key finding 2>

## Checks executed
- <List specific checks performed>

## Findings
(If none, write **None**. Otherwise use mcp-review-comment blocks:)

```mcp-review-comment
{"file":"path/to/file.cs","start_line":123,"end_line":125,"severity":"high|medium|low|info","tags":["functionality","pr-{{pr_number}}"],"body":"Problem → Why it matters → Concrete fix."}
```
```

## Severity guidelines
- **High**: Code doesn't work as intended, crashes, data loss possible
- **Medium**: Partial functionality, edge cases broken, degraded experience
- **Low**: Minor issues, cosmetic problems, suboptimal but working
- **Info**: Suggestions for improvement, not blocking

## External references (MUST research)
Before completing this step, fetch and check the PR against these authoritative sources:

| Reference | URL | Check for |
| --- | --- | --- |
| C# Design Guidelines | https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions | Coding conventions violations |
| .NET API Design | https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/ | API design issues |
| PowerToys Module Interface | `doc/devdocs/modules/interface.md` | Module contract violations |

**Enforcement**: In the output file, include a `## References consulted` section listing which guidelines were checked and any violations found.
