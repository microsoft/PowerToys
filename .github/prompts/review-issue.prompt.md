agent: 'agent'
model: GPT-5.1-Codex-Max
description: "You are a GitHub issue review and planning expert; score (0-100) and write one implementation plan. Outputs: overview.md, implementation-plan.md."
---

# GOAL
For **#{{issue_number}}** produce:
1) `Generated Files/issueReview/{{issue_number}}/overview.md`
2) `Generated Files/issueReview/{{issue_number}}/implementation-plan.md`

## Inputs
Figure out required inputs {{issue_number}} from the invocation context; if anything is missing, ask for the value or note it as a gap.

# CONTEXT (brief)
Ground evidence using `gh issue view {{issue_number}} --json number,title,body,author,createdAt,updatedAt,state,labels,milestone,reactions,comments,linkedPullRequests`, and download images to better understand the issue context.
Locate source code in the current workspace; feel free to use `rg`/`git grep`. Link related issues and PRs.

# OVERVIEW.MD
## Summary
Issue, state, milestone, labels. **Signals**: 👍/❤️/👎, comment count, last activity, linked PRs.

## At-a-Glance Score Table
Present all ratings in a compact table for quick scanning:

| Dimension | Score | Assessment | Key Drivers |
|-----------|-------|------------|-------------|
| **A) Business Importance** | X/100 | Low/Medium/High | Top 2 factors with scores |
| **B) Community Excitement** | X/100 | Low/Medium/High | Top 2 factors with scores |
| **C) Technical Feasibility** | X/100 | Low/Medium/High | Top 2 factors with scores |
| **D) Requirement Clarity** | X/100 | Low/Medium/High | Top 2 factors with scores |
| **Overall Priority** | X/100 | Low/Medium/High/Critical | Average or weighted summary |
| **Effort Estimate** | X days (T-shirt) | XS/S/M/L/XL/XXL/Epic | Type: bug/feature/chore |
| **Similar Issues Found** | X open, Y closed | — | Quick reference to related work |
| **Potential Assignees** | @username, @username | — | Top contributors to module |

**Assessment bands**: 0-25 Low, 26-50 Medium, 51-75 High, 76-100 Critical

## Ratings (0–100) — add evidence & short rationale
### A) Business Importance
- Labels (priority/security/regression): **≤35**
- Milestone/roadmap: **≤25**
- Customer/contract impact: **≤20**
- Unblocks/platform leverage: **≤20**
### B) Community Excitement
- 👍+❤️ normalized: **≤45**
- Comment volume & unique participants: **≤25**
- Recent activity (≤30d): **≤15**
- Duplicates/related issues: **≤15**
### C) Technical Feasibility
- Contained surface/clear seams: **≤30**
- Existing patterns/utilities: **≤25**
- Risk (perf/sec/compat) manageable: **≤25**
- Testability & CI support: **≤20**
### D) Requirement Clarity
- Behavior/repro/constraints: **≤60**
- Non-functionals (perf/sec/i18n/a11y): **≤25**
- Decision owners/acceptance signals: **≤15**

## Effort
Days + **T-shirt** (XS 0.5–1d, S 1–2, M 2–4, L 4–7, XL 7–14, XXL 14–30, Epic >30).  
Type/level: bug/feature/chore/docs/refactor/test-only; severity/value tier.

## Suggested Actions
Provide actionable recommendations for issue triage and assignment:

### A) Requirement Clarification (if Clarity score <50)
**When Requirement Clarity (Dimension D) is Medium or Low:**
- Identify specific gaps in issue description: missing repro steps, unclear expected behavior, undefined acceptance criteria, missing non-functional requirements
- Draft 3-5 clarifying questions to post as issue comment
- Suggest additional information needed: screenshots, logs, environment details, OS version, PowerToys version, error messages
- If behavior is ambiguous, propose 2-3 interpretation scenarios and ask reporter to confirm
- Example questions:
  - "Can you provide exact steps to reproduce this issue?"
  - "What is the expected behavior vs. what you're actually seeing?"
  - "Does this happen on Windows 10, 11, or both?"
  - "Can you attach a screenshot or screen recording?"

### B) Correct Label Suggestions
- Analyze issue type, module, and severity to suggest missing or incorrect labels
- Recommend labels from: `Issue-Bug`, `Issue-Feature`, `Issue-Docs`, `Issue-Task`, `Priority-High`, `Priority-Medium`, `Priority-Low`, `Needs-Triage`, `Needs-Author-Feedback`, `Product-<ModuleName>`, etc.
- If Requirement Clarity is low (<50), add `Needs-Author-Feedback` label
- If current labels are incorrect or incomplete, provide specific label changes with rationale

### C) Find Similar Issues & Past Fixes
- Search for similar issues using `gh issue list --search "keywords" --state all --json number,title,state,closedAt`
- Identify patterns: duplicate issues, related bugs, or similar feature requests
- For closed issues, find linked PRs that fixed them: check `linkedPullRequests` in issue data
- Provide 3-5 examples of similar issues with format: `#<number> - <title> (closed by PR #<pr>)` or `(still open)`

### D) Identify Subject Matter Experts
- Use git blame/log to find who fixed similar issues in the past
- Search for PR authors who touched relevant files: `git log --all --format='%aN' -- <file_paths> | sort | uniq -c | sort -rn | head -5`
- Check issue/PR history for frequent contributors to the affected module
- Suggest 2-3 potential assignees with context: `@<username> - <reason>` (e.g., "fixed similar rendering bug in #12345", "maintains FancyZones module")

### E) Semantic Search for Related Work
- Use semantic_search tool to find similar issues, code patterns, or past discussions
- Search queries should include: issue keywords, module names, error messages, feature descriptions
- Cross-reference semantic results with GitHub issue search for comprehensive coverage

**Output format for Suggested Actions section in overview.md:**
```markdown
## Suggested Actions

### Clarifying Questions (if Clarity <50)
Post these questions as issue comment to gather missing information:
1. <question>
2. <question>
3. <question>

**Recommended label**: `Needs-Author-Feedback`

### Label Recommendations
- Add: `<label>` - <reason>
- Remove: `<label>` - <reason>
- Current labels are appropriate ✓

### Similar Issues Found
1. #<number> - <title> (<state>, closed by PR #<pr> on <date>)
2. #<number> - <title> (<state>)
...

### Potential Assignees
- @<username> - <reason>
- @<username> - <reason>

### Related Code/Discussions
- <semantic search findings>
```

# IMPLEMENTATION-PLAN.MD
1) **Problem Framing** — restate problem; current vs expected; scope boundaries.  
2) **Layers & Files** — layers (UI/domain/data/infra/build). For each, list **files/dirs to modify** and **new files** (exact paths + why). Prefer repo patterns; cite examples/PRs.  
3) **Pattern Choices** — reuse existing; if new, justify trade-offs & transition.  
4) **Fundamentals** (brief plan or N/A + reason):
- Performance (hot paths, allocs, caching/streaming)
- Security (validation, authN/Z, secrets, SSRF/XSS/CSRF)
- G11N/L10N (resources, number/date, pluralization)
- Compatibility (public APIs, formats, OS/runtime/toolchain)
- Extensibility (DI seams, options/flags, plugin points)
- Accessibility (roles, labels, focus, keyboard, contrast)
- SOLID & repo conventions (naming, folders, dependency direction)
5) **Logging & Exception Handling**
- Where to log; levels; structured fields; correlation/traces.
- What to catch vs rethrow; retries/backoff; user-visible errors.
- **Privacy**: never log secrets/PII; redaction policy.
6) **Telemetry (optional — business metrics only)**
- Events/metrics (name, when, props); success signal; privacy/sampling; dashboards/alerts.
7) **Risks & Mitigations** — flags/canary/shadow-write/config guards.  
8) **Task Breakdown (agent-ready)** — table (leave a blank line before the header so Markdown renders correctly):

| Task | Intent | Files/Areas | Steps | Tests (brief) | Owner (Agent/Human) | Human interaction needed? (why) |
|---|---|---|---|---|---|---|

9) **Tests to Add (only)**
- **Unit**: targets, cases (success/edge/error), mocks/fixtures, path, notes.  
- **UI** (if applicable): flows, locator strategy, env/data/flags, path, flake mitigation.