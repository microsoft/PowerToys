# Step 11: Documentation & Automation Review

**Goal**: Ensure documentation is updated and CI/automation changes are correct.

## Output file
`Generated Files/prReview/{{pr_number}}/11-docs-automation.md`

## Checks to execute

### Code documentation
- [ ] Are public APIs documented with XML comments?
- [ ] Are complex algorithms explained in comments?
- [ ] Are non-obvious implementation decisions documented?
- [ ] Are TODO comments actionable (with issue links)?

### README and user docs
- [ ] Is README updated for new features?
- [ ] Are user-facing docs updated in /doc?
- [ ] Are screenshots/GIFs updated if UI changed?
- [ ] Are keyboard shortcuts documented?

### Developer documentation
- [ ] Are architecture changes documented in devdocs?
- [ ] Are new modules documented in doc/devdocs/modules/?
- [ ] Are build instructions updated if needed?
- [ ] Are dependencies documented in NOTICE.md if added?

### API documentation
- [ ] Are breaking changes documented?
- [ ] Are new settings documented?
- [ ] Are GPO policies documented if added?
- [ ] Is DSC configuration documented if applicable?

### CI/CD changes
- [ ] Are pipeline changes tested and correct?
- [ ] Are build matrix updates appropriate?
- [ ] Are test configurations correct?
- [ ] Are deployment steps accurate?

### GitHub automation
- [ ] Are issue/PR templates updated if needed?
- [ ] Are labels appropriate for changes?
- [ ] Are workflow triggers correct?
- [ ] Are actions using pinned versions?

### Release documentation
- [ ] Is CHANGELOG impact clear from PR description?
- [ ] Are migration steps documented for breaking changes?
- [ ] Are known issues documented?

## PowerToys-specific documentation
- [ ] Is the Settings UI page documented for new features?
- [ ] Are hotkey defaults documented?
- [ ] Is integration with other modules documented?
- [ ] Are troubleshooting steps provided for complex features?

## File template
```md
# Documentation & Automation Review
**PR:** {{pr_number}} — Base:{{baseRefName}} Head:{{headRefName}}
**Review iteration:** {{iteration}}

## Iteration history
### Iteration {{iteration}}
- <Key finding 1>
- <Key finding 2>

## Checks executed
- <List specific documentation checks performed>

## Documentation coverage
| Area | Status | Notes |
|------|--------|-------|
| Code comments | ✅/⚠️/❌ | |
| README | ✅/⚠️/❌ | |
| User docs | ✅/⚠️/❌ | |
| Dev docs | ✅/⚠️/❌ | |
| CI/CD | ✅/⚠️/❌ | |

## Findings
```mcp-review-comment
{"file":"path/to/file.cs","start_line":123,"end_line":125,"severity":"high|medium|low|info","tags":["documentation","pr-{{pr_number}}"],"body":"Documentation gap → What's missing → Suggested content."}
```
```

## Severity guidelines
- **High**: Missing critical documentation, broken CI, undocumented breaking changes
- **Medium**: Incomplete documentation, outdated screenshots
- **Low**: Minor documentation improvements, typos
- **Info**: Documentation enhancement suggestions

## External references (MUST research)
Before completing this step, **fetch and analyze** these authoritative sources:

| Reference | URL | Check for |
| --- | --- | --- |
| Microsoft Writing Style | https://docs.microsoft.com/en-us/style-guide/ | Writing style compliance |
| XML Documentation | https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/ | XML doc tag usage |
| GitHub Actions | https://docs.github.com/en/actions/learn-github-actions | Workflow best practices |
| Azure Pipelines | https://docs.microsoft.com/en-us/azure/devops/pipelines/ | Pipeline patterns |

**Enforcement**: Include `## References consulted` section with documentation standards checked.
