# Step 06: Localization Review

**Goal**: Ensure all user-facing strings are properly externalized and localizable.

## Output file
`Generated Files/prReview/{{pr_number}}/06-localization.md`

## Checks to execute

### String externalization
- [ ] Are all user-facing strings in resource files (.resx/.resw)?
- [ ] Are no hardcoded strings in code for UI text?
- [ ] Are error messages externalized?
- [ ] Are tooltip texts externalized?
- [ ] Are log messages (user-visible) externalized?

### Resource file quality
- [ ] Do resource keys follow naming conventions?
- [ ] Are resource comments provided for translator context?
- [ ] Are pluralization rules handled correctly?
- [ ] Are format strings using numbered placeholders ({0}, {1})?
- [ ] Are resource strings free of concatenation that breaks translation?

### String formatting
- [ ] Are sentences not built by concatenating fragments?
- [ ] Can translated strings accommodate different word orders?
- [ ] Are format placeholders documented for translators?
- [ ] Are gender-neutral alternatives provided where needed?

### UI layout
- [ ] Can UI accommodate longer translated strings (30-40% expansion)?
- [ ] Are text containers using dynamic sizing?
- [ ] Are truncation/ellipsis handled gracefully?
- [ ] Are fixed-width elements avoided for text?

### Images and icons
- [ ] Are images with text localized or text-free?
- [ ] Are culturally neutral icons used?
- [ ] Are icon tooltips externalized?

### Dates, numbers, currencies
- [ ] Are dates formatted using culture-aware formatting?
- [ ] Are numbers formatted using culture settings?
- [ ] Are currencies handled with proper symbols and placement?
- [ ] Are measurement units localizable?

## PowerToys-specific checks
- [ ] Are new strings added to Resources.resx (C#) or .rc files (C++)?
- [ ] Are module names/descriptions localizable?
- [ ] Are Settings UI strings in the correct resource file?
- [ ] Are context menu strings externalized?
- [ ] Are notification messages localizable?
- [ ] Is the update changelog localizable?

## Common issues to flag
```csharp
// BAD: Hardcoded string
MessageBox.Show("Operation completed");

// GOOD: Resource string
MessageBox.Show(Resources.OperationCompleted);

// BAD: Concatenated sentence
string msg = "Found " + count + " items in " + folder;

// GOOD: Format string
string msg = string.Format(Resources.FoundItemsInFolder, count, folder);
```

## File template
```md
# Localization Review
**PR:** {{pr_number}} — Base:{{baseRefName}} Head:{{headRefName}}
**Review iteration:** {{iteration}}

## Iteration history
### Iteration {{iteration}}
- <Key finding 1>
- <Key finding 2>

## Checks executed
- <List specific checks performed>

## Findings
```mcp-review-comment
{"file":"path/to/file.cs","start_line":123,"end_line":125,"severity":"high|medium|low|info","tags":["localization","pr-{{pr_number}}"],"body":"Hardcoded string found → Why it matters → Move to resources."}
```
```

## Severity guidelines
- **High**: User-facing hardcoded strings, broken UI due to text length
- **Medium**: Missing translator comments, concatenated sentences
- **Low**: Minor localizability improvements
- **Info**: Best practice suggestions for future localization

## External references (MUST research)
Before completing this step, **fetch and analyze** these authoritative sources:

| Reference | URL | Check for |
| --- | --- | --- |
| .NET Localization | https://docs.microsoft.com/en-us/dotnet/core/extensions/localization | Resource file best practices |
| Microsoft Style Guide | https://docs.microsoft.com/en-us/style-guide/global-communications/ | Writing for translation |
| Pseudo-localization | https://docs.microsoft.com/en-us/globalization/methodology/pseudolocalization | Testing localizability |

**Enforcement**: Include `## References consulted` section with guidelines checked and violations found.
