# Step 09: SOLID Principles Review

**Goal**: Evaluate adherence to SOLID design principles for maintainable, testable code.

## Output file
`Generated Files/prReview/{{pr_number}}/09-solid-design.md`

## Checks to execute

### Single Responsibility Principle (SRP)
- [ ] Does each class have one reason to change?
- [ ] Are classes focused on a single concern?
- [ ] Are methods doing one thing well?
- [ ] Are "God classes" avoided (classes doing too much)?
- [ ] Is business logic separated from UI/infrastructure?

### Open/Closed Principle (OCP)
- [ ] Is code open for extension, closed for modification?
- [ ] Can behavior be extended without changing existing code?
- [ ] Are switch statements on types avoided (use polymorphism)?
- [ ] Are configuration changes preferred over code changes?

### Liskov Substitution Principle (LSP)
- [ ] Can derived classes substitute base classes without issues?
- [ ] Are virtual method contracts honored?
- [ ] Are preconditions not strengthened in subtypes?
- [ ] Are postconditions not weakened in subtypes?
- [ ] Are exceptions not thrown for inherited behaviors?

### Interface Segregation Principle (ISP)
- [ ] Are interfaces focused and cohesive?
- [ ] Are clients forced to depend on methods they don't use?
- [ ] Are fat interfaces split into smaller ones?
- [ ] Is "interface pollution" avoided?

### Dependency Inversion Principle (DIP)
- [ ] Do high-level modules depend on abstractions?
- [ ] Are dependencies injected, not created internally?
- [ ] Are concrete implementations hidden behind interfaces?
- [ ] Is dependency injection container used consistently?

## Additional design checks

### Coupling and cohesion
- [ ] Is coupling minimized between modules?
- [ ] Is cohesion maximized within modules?
- [ ] Are circular dependencies avoided?
- [ ] Are package/namespace dependencies sensible?

### Testability
- [ ] Are classes easily unit-testable?
- [ ] Are external dependencies mockable?
- [ ] Is static state minimized?
- [ ] Are seams available for test doubles?

### Code organization
- [ ] Is code organized by feature or layer appropriately?
- [ ] Are naming conventions followed?
- [ ] Are access modifiers appropriate (not over-exposing)?
- [ ] Is the public API surface minimal?

## PowerToys-specific patterns
```csharp
// GOOD: DIP in module interface
public class ColorPickerModule : IModule {
    private readonly ISettingsReader _settings;
    public ColorPickerModule(ISettingsReader settings) {
        _settings = settings;
    }
}

// BAD: Tight coupling to concrete implementation
public class ColorPickerModule : IModule {
    private Settings _settings = new Settings(); // Hard to test
}

// GOOD: SRP - separate concerns
public class HotkeyManager { ... }  // Manages hotkeys
public class ColorCapture { ... }   // Captures colors
public class ClipboardService { ... } // Clipboard operations

// BAD: God class doing everything
public class ColorPicker {
    void RegisterHotkey() { ... }
    void CaptureScreen() { ... }
    void CopyToClipboard() { ... }
    void ShowUI() { ... }
    void SaveSettings() { ... }
}
```

## File template
```md
# SOLID Design Review
**PR:** {{pr_number}} — Base:{{baseRefName}} Head:{{headRefName}}
**Review iteration:** {{iteration}}

## Iteration history
### Iteration {{iteration}}
- <Key finding 1>
- <Key finding 2>

## Checks executed
- <List specific SOLID checks performed>

## Findings
```mcp-review-comment
{"file":"path/to/file.cs","start_line":123,"end_line":125,"severity":"high|medium|low|info","tags":["solid-design","pr-{{pr_number}}"],"body":"SOLID violation → Principle affected → Refactoring suggestion."}
```
```

## Severity guidelines
- **High**: Major design violation making code unmaintainable/untestable
- **Medium**: Moderate coupling/cohesion issues, testing difficulties
- **Low**: Minor design improvements, polish opportunities
- **Info**: Design pattern suggestions, best practice recommendations

## External references (MUST research)
Before completing this step, **fetch and analyze** these authoritative sources:

| Reference | URL | Check for |
| --- | --- | --- |
| SOLID Principles | https://docs.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles | SOLID violations |
| Clean Architecture | https://docs.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures | Architecture patterns |
| Dependency Injection | https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection | DI best practices |
| Design Patterns | https://refactoring.guru/design-patterns | Pattern applicability |

**Enforcement**: Include `## References consulted` section with specific SOLID principle checks and violations.
