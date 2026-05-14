# Step 12: Code Comments Review

**Goal**: Evaluate the quality and appropriateness of code comments.

## Output file
`Generated Files/prReview/{{pr_number}}/12-code-comments.md`

## Checks to execute

### Comment quality
- [ ] Do comments explain "why" not just "what"?
- [ ] Are comments accurate and up-to-date with code?
- [ ] Are comments concise and clear?
- [ ] Do comments add value beyond obvious code?
- [ ] Are comments free of redundant information?

### XML documentation (C#)
- [ ] Do public members have `<summary>` tags?
- [ ] Are `<param>` tags provided for parameters?
- [ ] Are `<returns>` tags provided for return values?
- [ ] Are `<exception>` tags documenting thrown exceptions?
- [ ] Are `<remarks>` used for additional context?

### Doxygen/comments (C++)
- [ ] Are public functions documented?
- [ ] Are complex macros documented?
- [ ] Are struct/class members documented?
- [ ] Are file headers present with copyright?

### TODO/FIXME comments
- [ ] Are TODOs actionable with clear description?
- [ ] Are TODOs linked to issues where appropriate?
- [ ] Are FIXMEs addressed or tracked?
- [ ] Are HACKs explained with justification?

### Region/section comments
- [ ] Are regions used appropriately (not excessively)?
- [ ] Do region names describe their content?
- [ ] Are large files organized with clear sections?

### Comment anti-patterns to flag
```csharp
// BAD: Obvious comment
i++; // Increment i

// BAD: Outdated comment (code does something else)
// Returns the sum of a and b
public int Subtract(int a, int b) => a - b;

// BAD: Commented-out code
// var oldImplementation = DoOldThing();
var newImplementation = DoNewThing();

// BAD: Vague TODO
// TODO: Fix this

// GOOD: Explains WHY
// We use a StringBuilder here because profiling showed
// string concatenation was a bottleneck with large file lists
var sb = new StringBuilder();

// GOOD: Actionable TODO
// TODO(#12345): Replace with async version when upgrading to .NET 8

// GOOD: Documents non-obvious behavior
// Win32 API returns -1 on error, not 0
if (result == -1) { ... }
```

### Special comment patterns
- [ ] Are license headers present where required?
- [ ] Are copyright notices correct?
- [ ] Are suppression comments (pragma) justified?
- [ ] Are platform-specific code blocks clearly marked?

## PowerToys-specific patterns
```csharp
// GOOD: Explains integration point
// The Runner calls this method when the hotkey is pressed.
// We must respond within 100ms to avoid the "not responding" UI.
public void OnHotkey() { ... }

// GOOD: Documents settings behavior
// This setting is persisted in JSON and synced with Settings UI.
// Changes require module restart to take effect.
[JsonPropertyName("activation_threshold")]
public int ActivationThreshold { get; set; }
```

## File template
```md
# Code Comments Review
**PR:** {{pr_number}} — Base:{{baseRefName}} Head:{{headRefName}}
**Review iteration:** {{iteration}}

## Iteration history
### Iteration {{iteration}}
- <Key finding 1>
- <Key finding 2>

## Checks executed
- <List specific comment checks performed>

## Comment quality summary
| Aspect | Assessment |
|--------|------------|
| Accuracy | ✅/⚠️/❌ |
| Completeness | ✅/⚠️/❌ |
| Clarity | ✅/⚠️/❌ |
| XML docs | ✅/⚠️/❌ |

## Findings
```mcp-review-comment
{"file":"path/to/file.cs","start_line":123,"end_line":125,"severity":"high|medium|low|info","tags":["code-comments","pr-{{pr_number}}"],"body":"Comment issue → Why it matters → Suggested fix."}
```
```

## Severity guidelines
- **High**: Misleading/incorrect comments, missing critical documentation
- **Medium**: Missing XML docs on public API, outdated comments
- **Low**: Minor comment improvements, clarity enhancements
- **Info**: Comment style suggestions

## External references (MUST research)
Before completing this step, **fetch and analyze** these authoritative sources:

| Reference | URL | Check for |
| --- | --- | --- |
| XML Documentation | https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/ | XML tag usage |
| Code Comments Guide | https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions#commenting-conventions | Comment conventions |
| Doxygen (C++) | https://www.doxygen.nl/manual/docblocks.html | C++ documentation |

**Enforcement**: Include `## References consulted` section with comment standards checked.
