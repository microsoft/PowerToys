# Step 08: Extensibility Review

**Goal**: Evaluate whether the code design supports future extension and customization without modification.

## Output file
`Generated Files/prReview/{{pr_number}}/08-extensibility.md`

## Checks to execute

### Plugin/module architecture
- [ ] Are extension points clearly defined?
- [ ] Is the plugin interface stable and versioned?
- [ ] Can plugins be added without recompiling core?
- [ ] Are plugin dependencies properly isolated?
- [ ] Is plugin discovery mechanism robust?

### Configuration extensibility
- [ ] Are magic numbers externalized to configuration?
- [ ] Are feature behaviors configurable?
- [ ] Can settings schema be extended without breaking changes?
- [ ] Are defaults sensible while allowing customization?

### Event-driven extensibility
- [ ] Are events exposed for key extension points?
- [ ] Is event subscription/unsubscription balanced?
- [ ] Are events strongly-typed (not object-based)?
- [ ] Can event handlers be added externally?

### Template/strategy patterns
- [ ] Are algorithms pluggable via interfaces?
- [ ] Are formatting rules customizable?
- [ ] Are processing pipelines extensible?
- [ ] Can new types be added without modifying existing code?

### API design
- [ ] Are public APIs minimal but sufficient?
- [ ] Are extension methods used appropriately?
- [ ] Is internal implementation hidden from extensions?
- [ ] Are breaking changes to public API avoided?

### Data format extensibility
- [ ] Are data formats versioned?
- [ ] Can formats be extended with new fields?
- [ ] Are unknown fields ignored gracefully (forward compatibility)?
- [ ] Is schema validation flexible?

## PowerToys-specific checks
- [ ] Does the module interface support new capability flags?
- [ ] Can PowerToys Run plugins extend functionality?
- [ ] Are preview handlers pluggable for new file types?
- [ ] Can FancyZones layouts be user-defined?
- [ ] Is the Settings UI extensible for new modules?
- [ ] Can themes/styles be customized?

## Design patterns to look for
```csharp
// GOOD: Strategy pattern for extensibility
public interface ISearchProvider { ... }
public class FileSearchProvider : ISearchProvider { ... }

// GOOD: Event-based extension point
public event EventHandler<FileChangedEventArgs> FileChanged;

// GOOD: Factory pattern for pluggable creation
public interface IPreviewHandlerFactory { ... }

// BAD: Hard-coded switch on type
switch (fileType) {
    case ".txt": ...
    case ".pdf": ...
    // Adding new type requires modifying this code
}
```

## File template
```md
# Extensibility Review
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
{"file":"path/to/file.cs","start_line":123,"end_line":125,"severity":"high|medium|low|info","tags":["extensibility","pr-{{pr_number}}"],"body":"Extensibility concern → Impact on future development → Suggested pattern."}
```
```

## Severity guidelines
- **High**: Breaking change to plugin interface, extension point removed
- **Medium**: Missed extension opportunity, tight coupling introduced
- **Low**: Minor extensibility improvements possible
- **Info**: Design suggestions for better extensibility

## External references (MUST research)
Before completing this step, **fetch and analyze** these authoritative sources:

| Reference | URL | Check for |
| --- | --- | --- |
| Plugin Architecture | https://docs.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support | Plugin loading patterns |
| Semantic Versioning | https://semver.org/ | Breaking change detection |
| PowerToys Module Interface | `doc/devdocs/modules/interface.md` | Contract compliance |
| Run Plugin API | `doc/devdocs/modules/launcher/plugins.md` | Plugin extension points |

**Enforcement**: Include `## References consulted` section with guidelines checked and violations found.
