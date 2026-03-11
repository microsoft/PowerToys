# Step 10: Repository Patterns Review

**Goal**: Ensure changes follow established PowerToys repository conventions and patterns.

## Output file
`Generated Files/prReview/{{pr_number}}/10-repo-patterns.md`

## Checks to execute

### Code style compliance
- [ ] Does C# code follow src/.editorconfig rules?
- [ ] Does C++ code follow src/.clang-format?
- [ ] Is XAML formatted per XamlStyler settings?
- [ ] Are naming conventions followed (PascalCase, camelCase)?

### Project structure
- [ ] Are new files in the correct project/folder?
- [ ] Is the module structure consistent with existing modules?
- [ ] Are shared utilities in common libraries, not duplicated?
- [ ] Are test projects properly named (*UnitTests, *UITests)?

### Settings patterns
- [ ] Are settings defined in the module's settings.cs?
- [ ] Is the settings JSON schema following the pattern?
- [ ] Are settings exposed through Settings UI correctly?
- [ ] Is settings versioning/migration handled?

### Logging patterns
- [ ] Is spdlog used for C++ logging?
- [ ] Is Logger class used for C# logging?
- [ ] Are log levels appropriate (no spam in release)?
- [ ] Are sensitive values not logged?
- [ ] Is logging following repo guidelines?

### IPC patterns
- [ ] Is named pipe communication using established helpers?
- [ ] Are IPC message formats JSON with proper schema?
- [ ] Are IPC operations async and timeout-protected?

### Resource patterns
- [ ] Are resources in the correct .resx/.rc files?
- [ ] Is resource naming following conventions?
- [ ] Are PRI files configured correctly for WinUI?

### Build patterns
- [ ] Are project references used (not DLL references)?
- [ ] Are package versions from Directory.Packages.props?
- [ ] Is the project included in the solution correctly?
- [ ] Are build configurations consistent?

### Error handling patterns
- [ ] Are exceptions caught at appropriate boundaries?
- [ ] Is exception information logged properly?
- [ ] Are user-facing errors localized?
- [ ] Is graceful degradation preferred over crashing?

## PowerToys-specific patterns
```csharp
// Settings pattern
public class MyModuleSettings : BasePTModuleSettings {
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; } = true;
}

// Module interface pattern
public class MyModule : IModule {
    public string Name => "MyModule";
    public string GetKey() => "MyModule";
    // ... implement interface
}

// Logging pattern (C#)
Logger.LogInfo("Operation completed");
Logger.LogError("Failed: {0}", ex.Message);

// Logging pattern (C++)
Logger::info("Operation completed");
Logger::error("Failed: {}", errorMsg);
```

## Files to reference
- Architecture: `doc/devdocs/core/architecture.md`
- Coding style: `doc/devdocs/development/style.md`
- Logging: `doc/devdocs/development/logging.md`
- Module interface: `doc/devdocs/modules/interface.md`

## File template
```md
# Repository Patterns Review
**PR:** {{pr_number}} — Base:{{baseRefName}} Head:{{headRefName}}
**Review iteration:** {{iteration}}

## Iteration history
### Iteration {{iteration}}
- <Key finding 1>
- <Key finding 2>

## Checks executed
- <List specific pattern checks performed>

## Findings
```mcp-review-comment
{"file":"path/to/file.cs","start_line":123,"end_line":125,"severity":"high|medium|low|info","tags":["repo-patterns","pr-{{pr_number}}"],"body":"Pattern deviation → Repo convention reference → How to fix."}
```
```

## Severity guidelines
- **High**: Major deviation from required patterns, will cause build/integration issues
- **Medium**: Pattern inconsistency, makes codebase harder to maintain
- **Low**: Minor style issues, naming improvements
- **Info**: Suggestions for better alignment with repo conventions

## External references (MUST research)
Before completing this step, **fetch and analyze** these local documentation files:

| Reference | Path | Check for |
| --- | --- | --- |
| Architecture | `doc/devdocs/core/architecture.md` | Module structure compliance |
| Coding Style | `doc/devdocs/development/style.md` | Style guide adherence |
| Logging Guidelines | `doc/devdocs/development/logging.md` | Logging pattern compliance |
| Module Interface | `doc/devdocs/modules/interface.md` | Interface contract |
| AGENTS.md | `AGENTS.md` | AI contributor guidelines |

**Enforcement**: Include `## References consulted` section with repo docs checked and deviations found.
