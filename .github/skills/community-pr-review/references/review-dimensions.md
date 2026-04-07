# Review Dimensions Reference

Detailed criteria for each of the 7 review dimensions used in community bug-fix PR review.

## 1. Correctness

**Goal**: Verify the fix solves the reported bug without introducing regressions.

### Checklist
- [ ] Fix addresses the root cause described in the linked issue
- [ ] All code paths to the bug are covered
- [ ] Edge cases handled: null, empty, boundary values, max/min, concurrent access
- [ ] No new bugs introduced by the fix
- [ ] Tests added or updated to cover the fix
- [ ] Fix is complete (not partial — all scenarios in the bug report addressed)
- [ ] Conditional branches handle all expected cases
- [ ] Loops terminate correctly (no infinite loops, off-by-one errors)
- [ ] State properly initialized, used, and cleaned up

### PowerToys-Specific
- [ ] Module interface contract remains intact
- [ ] Hotkey registration and unregistration balanced
- [ ] Feature works correctly with Runner lifecycle (enable/disable)
- [ ] Settings UI changes reflected in module behavior

### Severity Guide
| Severity | Criteria |
|----------|----------|
| High | Fix doesn't solve the bug, introduces crash, data loss |
| Medium | Partial fix, edge cases broken, degraded UX |
| Low | Minor issues, cosmetic, suboptimal but working |
| Info | Suggestions for improvement |

---

## 2. Security

**Goal**: Identify security vulnerabilities and unsafe practices.

### Checklist
- [ ] User input validated before use
- [ ] File paths canonicalized (no `..` traversal)
- [ ] Shell commands avoided or properly escaped
- [ ] Elevation (UAC) used only when necessary, scoped minimally
- [ ] Credentials, tokens, PII never logged or exposed
- [ ] Temporary files created securely
- [ ] Named pipes/shared memory secured with ACLs
- [ ] IPC messages validated before processing
- [ ] DLL search paths secured
- [ ] No format string vulnerabilities

### Native Code (C/C++)
- [ ] Buffer overflow prevention
- [ ] P/Invoke signatures correct (buffer sizes)
- [ ] Memory zeroed before freeing (for secrets)
- [ ] No use-after-free patterns
- [ ] RAII patterns for resource management

### Severity Guide
| Severity | Criteria |
|----------|----------|
| High | RCE, privilege escalation, data breach possible |
| Medium | Local exploit, information disclosure, weak crypto |
| Low | Defense in depth improvement, hardening opportunity |
| Info | Security best practice suggestions |

### References
- OWASP Top 10: https://owasp.org/www-project-top-ten/
- CWE Top 25: https://cwe.mitre.org/top25/
- Microsoft SDL: https://www.microsoft.com/en-us/securityengineering/sdl

---

## 3. Performance

**Goal**: Ensure no performance regressions, especially in hot paths.

### Checklist
- [ ] Hot paths (hooks, tight loops, event handlers) kept efficient
- [ ] No unnecessary allocations in frequently called code
- [ ] Async patterns correct (no sync-over-async, proper cancellation tokens)
- [ ] Collections appropriately sized (no repeated resizing)
- [ ] Expensive operations (file I/O, registry, network) minimized
- [ ] No logging in performance-critical paths
- [ ] LINQ used appropriately (no repeated enumeration)
- [ ] String operations efficient (StringBuilder for loops)
- [ ] No blocking on UI thread

### PowerToys-Specific
- [ ] Hook callbacks execute quickly (< 1ms)
- [ ] Settings reads cached appropriately
- [ ] Module enable/disable is fast
- [ ] No startup time regression

### Severity Guide
| Severity | Criteria |
|----------|----------|
| High | Noticeable lag, UI freeze, O(n²) in common path |
| Medium | Subtle perf regression, unnecessary work |
| Low | Minor optimization opportunity |
| Info | Performance best practice |

---

## 4. Reliability

**Goal**: Verify robust error handling and resource management.

### Checklist
- [ ] Errors handled gracefully (try/catch, HRESULT checks, null guards)
- [ ] Resources properly disposed (IDisposable, COM objects, handles)
- [ ] Race conditions prevented (thread safety, proper locking)
- [ ] Event subscriptions balanced (subscribe ↔ unsubscribe)
- [ ] Process/module lifecycle handled correctly
- [ ] Retries and timeouts appropriate
- [ ] Graceful degradation on failure (don't crash the whole app)
- [ ] Exception types appropriate (not catching all Exception)
- [ ] Finalizers/destructors not relying on other managed objects

### PowerToys-Specific
- [ ] Module crash doesn't take down Runner
- [ ] Named pipe disconnection handled
- [ ] Settings file corruption handled
- [ ] Multi-monitor/DPI changes handled

### Severity Guide
| Severity | Criteria |
|----------|----------|
| High | Crash, hang, resource leak causing system impact |
| Medium | Intermittent failure, poor error recovery |
| Low | Missing error handling in rare path |
| Info | Reliability improvement suggestion |

---

## 5. Design

**Goal**: Assess code quality and architectural fit.

### Checklist
- [ ] Fix appropriately scoped (not over-engineered)
- [ ] SOLID principles followed
- [ ] Abstraction level appropriate
- [ ] Could be simpler while still correct?
- [ ] No code smells (magic numbers, god methods, deep nesting)
- [ ] Fix is in the right layer/module
- [ ] No unnecessary coupling introduced
- [ ] API surface changes are intentional and minimal

### Severity Guide
| Severity | Criteria |
|----------|----------|
| High | Architectural violation, wrong abstraction layer |
| Medium | Design smell, maintainability concern |
| Low | Minor refactoring opportunity |
| Info | Design improvement suggestion |

---

## 6. Compatibility

**Goal**: Ensure no breaking changes or compatibility issues.

### Checklist
- [ ] No breaking changes to public APIs
- [ ] IPC contracts (named pipes, JSON) preserved
- [ ] Backward compatibility for settings/config files
- [ ] Works across Windows 10 1803+ and Windows 11
- [ ] Installer/upgrade path not affected
- [ ] If modifying `src/common/`: ABI stability preserved
- [ ] Schema migrations handled (if settings format changed)
- [ ] GPO/policy paths not broken

### Severity Guide
| Severity | Criteria |
|----------|----------|
| High | Breaking change, data loss on upgrade |
| Medium | Partial compat issue, workaround exists |
| Low | Minor compat concern, future risk |
| Info | Compatibility best practice |

---

## 7. Repo Patterns

**Goal**: Verify adherence to PowerToys conventions.

### Checklist
- [ ] Naming conventions followed (check `.editorconfig`, `.clang-format`)
- [ ] Style consistent with surrounding code
- [ ] New strings localized (`.resx` files)
- [ ] Logging follows pattern (spdlog for C++, Logger for C#)
- [ ] Module interface contracts preserved
- [ ] PR is atomic (one logical change)
- [ ] No drive-by refactors mixed in
- [ ] New dependencies listed in `NOTICE.md`
- [ ] Test coverage appropriate

### Style References
- C#: `src/.editorconfig`, StyleCop.Analyzers
- C++: `src/.clang-format`
- XAML: XamlStyler

### Severity Guide
| Severity | Criteria |
|----------|----------|
| High | Contract violation, ABI break |
| Medium | Convention violation, inconsistent pattern |
| Low | Minor style issue |
| Info | Pattern improvement suggestion |
