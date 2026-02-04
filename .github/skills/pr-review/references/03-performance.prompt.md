# Step 03: Performance Review

**Goal**: Identify performance regressions, inefficiencies, and resource management issues.

## Output file
`Generated Files/prReview/{{pr_number}}/03-performance.md`

## Checks to execute

### CPU efficiency
- [ ] Are there unnecessary loops or repeated calculations?
- [ ] Are LINQ queries efficient (avoiding multiple enumerations)?
- [ ] Are regular expressions compiled if used frequently?
- [ ] Are string operations using StringBuilder for concatenation?
- [ ] Are hot paths optimized (avoid logging, allocations)?

### Memory management
- [ ] Are IDisposable objects properly disposed?
- [ ] Are event handlers unsubscribed to prevent leaks?
- [ ] Are large objects pooled or reused where appropriate?
- [ ] Are caches bounded to prevent unbounded growth?
- [ ] Are WeakReferences used for optional caches?

### Async/threading
- [ ] Are async methods truly asynchronous (not blocking)?
- [ ] Is ConfigureAwait(false) used in library code?
- [ ] Are locks held for minimal duration?
- [ ] Are thread-safe collections used for shared data?
- [ ] Are cancellation tokens propagated correctly?

### I/O efficiency
- [ ] Are file operations buffered appropriately?
- [ ] Are network calls batched where possible?
- [ ] Is file watching efficient (not polling)?
- [ ] Are settings read/written efficiently (not on every keystroke)?

### UI responsiveness
- [ ] Are long operations off the UI thread?
- [ ] Is virtualization used for large lists?
- [ ] Are images loaded asynchronously?
- [ ] Are animations smooth (60fps target)?
- [ ] Is UI updated efficiently (batch updates, not per-item)?

### Startup performance
- [ ] Are modules lazy-loaded where possible?
- [ ] Is initialization parallelized where safe?
- [ ] Are expensive operations deferred until needed?
- [ ] Is the critical path to first interaction minimized?

## PowerToys-specific checks
- [ ] Does the module minimize CPU when idle (no busy loops)?
- [ ] Are global hooks efficient (minimal processing in callback)?
- [ ] Are IPC messages batched/throttled appropriately?
- [ ] Does the module release resources when disabled?
- [ ] Are thumbnail/preview generations cached?

## File template
```md
# Performance Review
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
{"file":"path/to/file.cs","start_line":123,"end_line":125,"severity":"high|medium|low|info","tags":["performance","pr-{{pr_number}}"],"body":"Problem → Impact estimate → Concrete fix."}
```
```

## Severity guidelines
- **High**: Significant CPU/memory regression, UI freezes, memory leaks
- **Medium**: Noticeable slowdown, inefficient algorithm, unbounded growth
- **Low**: Minor inefficiency, premature optimization opportunity
- **Info**: Performance improvement suggestions

## External references (MUST research)
Before completing this step, **fetch and analyze** these authoritative sources:

| Reference | URL | Check for |
| --- | --- | --- |
| .NET Performance Tips | https://docs.microsoft.com/en-us/dotnet/framework/performance/performance-tips | Anti-pattern violations |
| Async Best Practices | https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming | Async/await issues |
| Memory Management | https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/fundamentals | GC pressure patterns |
| WPF Performance | https://docs.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance | UI virtualization, binding |

**Enforcement**: Include `## References consulted` section listing checked guidelines and violations found.
