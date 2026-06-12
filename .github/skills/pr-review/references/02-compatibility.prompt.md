# Step 02: Compatibility Review

**Goal**: Ensure changes maintain compatibility with supported Windows versions, architectures, and don't introduce breaking changes.

## Output file
`Generated Files/prReview/{{pr_number}}/02-compatibility.md`

## Checks to execute

### Windows version compatibility
- [ ] Are Win32 APIs available on all supported Windows versions (10 1803+)?
- [ ] Are any APIs marked as Windows 11 only used conditionally?
- [ ] Are version checks in place for newer APIs?
- [ ] Are manifest compatibility settings correct?

### Architecture compatibility
- [ ] Does code work on both x64 and ARM64?
- [ ] Are pointer sizes handled correctly (IntPtr vs int)?
- [ ] Are P/Invoke signatures correct for both architectures?
- [ ] Are any architecture-specific paths handled?

### .NET compatibility
- [ ] Are target frameworks consistent across projects?
- [ ] Are nullable reference types handled correctly?
- [ ] Are any APIs deprecated in target .NET version?
- [ ] Do AOT-compiled components avoid reflection issues?

### Breaking changes
- [ ] Are settings schema changes backward compatible?
- [ ] Are IPC message formats versioned/compatible?
- [ ] Are file format changes backward compatible?
- [ ] Are public API signatures preserved?
- [ ] Are GPO policy keys unchanged or properly migrated?

### Dependency compatibility
- [ ] Are NuGet package versions compatible?
- [ ] Are native DLL dependencies available on all targets?
- [ ] Are any dependencies deprecated or end-of-life?
- [ ] Do WinUI/WPF versions match project requirements?

### Interoperability
- [ ] Are COM interfaces properly defined?
- [ ] Are shell extensions compatible with Explorer versions?
- [ ] Are context menu handlers working on Win10 and Win11?
- [ ] Are clipboard/drag-drop formats standard?

## PowerToys-specific checks
- [ ] Is the module interface version compatible?
- [ ] Do settings migrations handle all previous versions?
- [ ] Are hotkey codes platform-independent?
- [ ] Does the installer handle upgrades correctly?

## File template
```md
# Compatibility Review
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
{"file":"path/to/file.cs","start_line":123,"end_line":125,"severity":"high|medium|low|info","tags":["compatibility","pr-{{pr_number}}"],"body":"Problem → Why it matters → Concrete fix."}
```
```

## Severity guidelines
- **High**: Breaks on supported Windows version, crashes on ARM64, data migration failure
- **Medium**: Degraded functionality on some platforms, deprecated API usage
- **Low**: Minor compatibility warnings, future deprecation concerns
- **Info**: Suggestions for broader compatibility

## External references (MUST research)
Before completing this step, **fetch and analyze** these authoritative sources:

| Reference | URL | Check for |
| --- | --- | --- |
| Windows Version Info | https://docs.microsoft.com/en-us/windows/release-health/supported-versions-windows-client | Supported version requirements |
| .NET Breaking Changes | https://docs.microsoft.com/en-us/dotnet/core/compatibility/ | Breaking change patterns |
| Win32 API Availability | https://docs.microsoft.com/en-us/windows/win32/apiindex/windows-api-list | API version requirements |
| WinAppSDK Release Notes | https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/stable-channel | SDK compatibility notes |

**Enforcement**: Include `## References consulted` section listing checked guidelines and violations found.
