# Step 05: Security Review

**Goal**: Identify security vulnerabilities, unsafe practices, and potential attack vectors in the code changes.

## Output file
`Generated Files/prReview/{{pr_number}}/05-security.md`

## Checks to execute

### Input validation
- [ ] Is all user input validated before use?
- [ ] Are file paths validated and canonicalized?
- [ ] Are command-line arguments sanitized?
- [ ] Are URLs validated before navigation?
- [ ] Are numeric inputs bounds-checked?
- [ ] Is input length limited to prevent DoS?

### Injection vulnerabilities
- [ ] Is SQL/command injection prevented (parameterized queries)?
- [ ] Are shell commands avoided or properly escaped?
- [ ] Is path traversal prevented (no `..` in paths)?
- [ ] Are XAML/JSON inputs validated against injection?
- [ ] Are registry operations using safe APIs?

### Authentication & authorization
- [ ] Are admin operations protected appropriately?
- [ ] Is elevation (UAC) used only when necessary?
- [ ] Are privileged operations minimized in scope?
- [ ] Are credentials never logged or exposed?
- [ ] Are tokens/secrets stored securely?

### Data protection
- [ ] Is sensitive data encrypted at rest?
- [ ] Are secure channels used for network communication?
- [ ] Is PII handled according to privacy guidelines?
- [ ] Are temporary files created securely?
- [ ] Is data sanitized before logging?

### Memory safety
- [ ] Are buffer overflows prevented in native code?
- [ ] Are unsafe blocks minimized and reviewed?
- [ ] Are P/Invoke signatures correct (buffer sizes)?
- [ ] Is memory zeroed before freeing (for secrets)?
- [ ] Are format strings validated?

### Process security
- [ ] Are child processes started with minimal privileges?
- [ ] Are DLL search paths secured?
- [ ] Is code signing validated for loaded modules?
- [ ] Are named pipes/shared memory secured with ACLs?
- [ ] Are race conditions (TOCTOU) prevented?

### Cryptography
- [ ] Are modern algorithms used (no MD5/SHA1 for security)?
- [ ] Are random numbers cryptographically secure?
- [ ] Are keys of sufficient length?
- [ ] Is key derivation using proper KDFs?

## PowerToys-specific checks
- [ ] Do modules with elevated privileges minimize their scope?
- [ ] Are IPC messages validated before processing?
- [ ] Are hook callbacks resistant to malicious input?
- [ ] Are file preview handlers sandboxed appropriately?
- [ ] Are shell extensions checking caller identity?
- [ ] Is the GPO policy path secured?

## File template
```md
# Security Review
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
{"file":"path/to/file.cs","start_line":123,"end_line":125,"severity":"high|medium|low|info","tags":["security","pr-{{pr_number}}"],"body":"Vulnerability → Attack scenario → Concrete fix."}
```
```

## Severity guidelines
- **High**: Remote code execution, privilege escalation, data breach possible
- **Medium**: Local exploit, information disclosure, weak crypto
- **Low**: Defense in depth improvement, hardening opportunity
- **Info**: Security best practice suggestions

## External references (MUST research)
Before completing this step, **fetch and analyze** these authoritative sources against the PR changes:

| Reference | URL | Check for |
| --- | --- | --- |
| OWASP Top 10 | https://owasp.org/www-project-top-ten/ | Top 10 vulnerability patterns |
| Microsoft SDL | https://www.microsoft.com/en-us/securityengineering/sdl | SDL practice violations |
| CWE Top 25 | https://cwe.mitre.org/top25/ | Common weakness patterns |
| .NET Security | https://docs.microsoft.com/en-us/dotnet/standard/security/ | .NET security best practices |
| Input Validation | https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html | Input validation patterns |

**Enforcement**: In the output file, include a `## References consulted` section with:
- Which OWASP Top 10 items were checked (by ID: A01-A10)
- Which CWE patterns were verified
- Any violations found with specific CWE/OWASP references
