# Step 07: Globalization Review

**Goal**: Ensure the code works correctly across different cultures, locales, and regional settings.

## Output file
`Generated Files/prReview/{{pr_number}}/07-globalization.md`

## Checks to execute

### Text handling
- [ ] Is Unicode fully supported (emojis, CJK, RTL)?
- [ ] Are string comparisons culture-aware where needed?
- [ ] Are string comparisons ordinal where culture doesn't matter?
- [ ] Is text encoding handled correctly (UTF-8 preferred)?
- [ ] Are file paths supporting Unicode characters?

### Right-to-left (RTL) support
- [ ] Is UI layout RTL-aware (FlowDirection)?
- [ ] Are icons/images mirrored appropriately for RTL?
- [ ] Is text alignment correct for RTL languages?
- [ ] Are bidirectional text scenarios handled?

### Date and time
- [ ] Is DateTimeOffset used for cross-timezone scenarios?
- [ ] Are time zones handled correctly?
- [ ] Is calendar system (Gregorian vs others) considered?
- [ ] Are 12/24 hour formats culture-dependent?
- [ ] Is week start day culture-aware?

### Numbers and currency
- [ ] Is decimal separator culture-aware (, vs .)?
- [ ] Is thousands separator culture-aware?
- [ ] Is number grouping culture-aware (1,000 vs 10,00)?
- [ ] Are currency symbols positioned correctly per culture?
- [ ] Is negative number format culture-aware?

### Sorting and comparison
- [ ] Is sorting culture-aware where appropriate?
- [ ] Are collation rules respected?
- [ ] Is case conversion culture-aware (Turkish i issue)?
- [ ] Are string equality checks appropriate (ordinal vs culture)?

### Input methods
- [ ] Does text input work with IME (Input Method Editor)?
- [ ] Are keyboard shortcuts working with non-US layouts?
- [ ] Is clipboard handling encoding-aware?

### File system
- [ ] Are file paths normalized for cross-platform?
- [ ] Is path separator handled correctly?
- [ ] Are invalid filename characters culture-considered?

## PowerToys-specific checks
- [ ] Does PowerToys Run work with CJK input?
- [ ] Are hotkeys working with international keyboard layouts?
- [ ] Is file search supporting Unicode filenames?
- [ ] Are preview handlers rendering RTL content correctly?
- [ ] Is the Settings UI RTL-aware?

## Common issues to flag
```csharp
// BAD: Culture-sensitive comparison for identifiers
if (str.ToLower() == "value")

// GOOD: Ordinal comparison for identifiers  
if (str.Equals("value", StringComparison.OrdinalIgnoreCase))

// BAD: Implicit current culture
double.Parse(input)

// GOOD: Explicit culture for data
double.Parse(input, CultureInfo.InvariantCulture)

// BAD: Hardcoded date format
DateTime.ParseExact(s, "MM/dd/yyyy", null)

// GOOD: Culture-aware or ISO format
DateTime.Parse(s, CultureInfo.CurrentCulture)
```

## File template
```md
# Globalization Review
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
{"file":"path/to/file.cs","start_line":123,"end_line":125,"severity":"high|medium|low|info","tags":["globalization","pr-{{pr_number}}"],"body":"Culture issue → Affected regions → Concrete fix with CultureInfo."}
```
```

## Severity guidelines
- **High**: Crashes/data corruption in non-US locales, RTL completely broken
- **Medium**: Incorrect formatting, sorting issues, IME problems
- **Low**: Minor globalization improvements
- **Info**: Best practice suggestions for international users

## External references (MUST research)
Before completing this step, **fetch and analyze** these authoritative sources:

| Reference | URL | Check for |
| --- | --- | --- |
| .NET Globalization | https://docs.microsoft.com/en-us/dotnet/core/extensions/globalization | CultureInfo best practices |
| Unicode Bidirectional | https://unicode.org/reports/tr9/ | RTL text handling |
| ICU Guidelines | https://unicode-org.github.io/icu/userguide/ | International text processing |
| Date/Time Formatting | https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings | Format string patterns |

**Enforcement**: Include `## References consulted` section with guidelines checked and violations found.
