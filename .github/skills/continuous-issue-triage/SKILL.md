---
name: continuous-issue-triage
description: Automated issue triage assistant for periodic (daily/weekly) issue queue management. Use when asked to triage issues, review issue backlog, find trending issues, identify stale issues needing response, categorize unlabeled issues, find issues ready for fix, draft reply messages, check for issues needing clarification, find closeable issues after PR merge, or run periodic issue health checks. Supports both open and closed issues with activity tracking between runs.
license: Complete terms in LICENSE.txt
---

# Continuous Issue Triage Skill

Automated periodic triage of GitHub issues to keep the issue queue healthy. Designed to run daily, twice-weekly, or weekly, tracking activity between runs and categorizing issues by actionable priority.

## Output Directory

All artifacts are placed under `Generated Files/triage-issues/` at the repository root (gitignored).

```
Generated Files/triage-issues/
├── triage-state.json            # Persistent state between runs
├── current-run/
│   ├── summary.md               # Executive summary for this run
│   ├── trending.md              # Trending issues report
│   ├── needs-label.md           # Issues missing area labels
│   ├── ready-for-fix.md         # Issues confident for fix
│   ├── needs-info.md            # Issues needing author feedback
│   ├── needs-clarification.md   # Clarification requests (not bugs)
│   ├── closeable.md             # Issues ready to close
│   └── draft-replies/           # Pre-drafted reply messages
│       └── issue-XXXXX.md
├── history/
│   └── YYYY-MM-DD/              # Historical run archives
└── issue-cache/                 # Cached issue reviews (reuse review-issue)
    └── XXXXX/
        ├── overview.md
        └── implementation-plan.md
```

## When to Use This Skill

- Run periodic triage (daily, twice-weekly, weekly)
- Find trending issues with high activity
- Identify unlabeled issues needing categorization
- Find issues ready for implementation
- Draft replies for issues needing clarification
- Identify closeable issues after PR merge/release
- Track follow-up actions between triage sessions
- Review closed issues with new comments

## Prerequisites

- GitHub CLI (`gh`) installed and authenticated
- MCP Server: github-mcp-server (optional, for images/attachments)
- Access to `.github/prompts/review-issue.prompt.md` for deep analysis

## Workflow Overview

```
┌─────────────────────────────────┐
│ 1. Load Previous State          │
│    (triage-state.json)          │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 2. Collect Active Issues        │
│    - Recently updated open      │
│    - Closed with new comments   │
│    - Previously flagged         │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 3. Categorize Issues            │
│    (Apply category rules)       │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 4. Deep Analysis (selective)    │
│    (Use review-issue prompt)    │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 5. Generate Reports & Drafts    │
└─────────────────────────────────┘
              ↓
┌─────────────────────────────────┐
│ 6. Save State for Next Run      │
└─────────────────────────────────┘
```

## Issue Categories

Issues are categorized into actionable buckets with prioritization scores:

| Category | Emoji | Criteria | Human Action |
|----------|-------|----------|--------------|
| **Trending** | 🔥 | 5+ new comments since last run | Review conversation, respond |
| **Needs-Label** | 🏷️ | Missing `Product-*` or `Area-*` label | Apply suggested label |
| **Ready-for-Fix** | ✅ | High clarity, feasible, validated | Assign or implement |
| **Needs-Info** | ❓ | Missing repro, impact, or expected result | Post drafted questions |
| **Needs-Clarification** | 💬 | Question/discussion, not a bug | Post explanation reply |
| **Closeable** | ✔️ | Fixed by PR, released, or resolved | Close with message |
| **Stale-Waiting** | ⏳ | Waiting on author >14 days | Ping or close |
| **Duplicate-Candidate** | 🔁 | Similar to existing issue | Link and close |

## Detailed Workflow Docs

Read steps progressively—only load what you need:

- [Step 1: State Management](./references/step1-state-management.md)
- [Step 2: Issue Collection](./references/step2-collection.md)
- [Step 3: Categorization Rules](./references/step3-categorization.md)
- [Step 4: Deep Analysis](./references/step4-deep-analysis.md)
- [Step 5: Report Generation](./references/step5-reports.md)
- [Step 6: Reply Templates](./references/step6-reply-templates.md)

## Available Scripts

| Script | Purpose |
|--------|---------|
| [run-triage.ps1](./scripts/run-triage.ps1) | **Main orchestrator** - runs full triage with parallel Copilot CLI |
| [collect-active-issues.ps1](./scripts/collect-active-issues.ps1) | Fetch issues updated since last run (standalone) |
| [categorize-issues.ps1](./scripts/categorize-issues.ps1) | Apply categorization rules (standalone) |
| [generate-summary.ps1](./scripts/generate-summary.ps1) | Create executive summary (standalone) |

## Quick Start

1. **First Run**: Creates initial state, analyzes recent activity
2. **Subsequent Runs**: Compares against previous state, highlights changes (delta)

### Running the Triage

**PowerShell 7 Required** - Uses parallel processing for efficiency.

```powershell
# Basic run (weekly, 5 parallel, 5min timeout, 3 retries)
.\.github\skills\continuous-issue-triage\scripts\run-triage.ps1

# Daily run with more parallelism
.\.github\skills\continuous-issue-triage\scripts\run-triage.ps1 -RunType daily -MaxParallel 10

# With specific model
.\.github\skills\continuous-issue-triage\scripts\run-triage.ps1 -Model "claude-sonnet-4"

# Force re-analyze all (ignore cache)
.\.github\skills\continuous-issue-triage\scripts\run-triage.ps1 -Force

# With MCP config
.\.github\skills\continuous-issue-triage\scripts\run-triage.ps1 -McpConfig ".\.github\mcp.json"
```

### Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-RunType` | weekly | daily, twice-weekly, weekly |
| `-MaxParallel` | 5 | Concurrent Copilot CLI invocations |
| `-TimeoutMinutes` | 5 | Timeout per issue analysis |
| `-MaxRetries` | 3 | Retries on timeout/failure |
| `-Model` | (default) | Copilot model to use |
| `-McpConfig` | (none) | Path to MCP config file |
| `-LookbackDays` | 7 | Days to look back on first run |
| `-Force` | false | Re-analyze all, ignore cache |

### Example Invocation (via Copilot Chat)

```
"Run issue triage" or "Triage issues for this week"
```

The skill will:
1. Check for existing `triage-state.json`
2. Collect issues updated since last run (or last 7 days for first run)
3. **Run parallel Copilot CLI analysis** with timeout/retry handling
4. Categorize and prioritize (using cached results where valid)
5. Generate actionable reports with draft replies
6. Save state for next run (delta tracking)

## Parallel Execution Model

The skill uses PowerShell 7's `ForEach-Object -Parallel` to analyze issues concurrently:

```
┌─────────────────────────────────────────────────────────────┐
│                    run-triage.ps1                           │
├─────────────────────────────────────────────────────────────┤
│  Issue #123 ──┐                                             │
│  Issue #124 ──┼── ForEach-Object -Parallel ─┬── Result #123 │
│  Issue #125 ──┤   (ThrottleLimit: 5)        ├── Result #124 │
│  Issue #126 ──┤                             ├── Result #125 │
│  Issue #127 ──┘                             └── Result #126 │
│                                                  ...        │
│  Each issue:                                                │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ copilot -p "Analyze #N..." --yolo                   │    │
│  │   ├── Timeout: 5 minutes                            │    │
│  │   ├── Retry: up to 3 times                          │    │
│  │   └── Output: JSON analysis result                  │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

### Timeout & Retry Handling

- Each Copilot CLI invocation has a **5 minute timeout** (configurable)
- On timeout: job is killed, waits 10 seconds, retries
- **3 retries maximum** before marking as failed
- Failed analyses are logged and reported separately

## Delta Tracking

The skill tracks state between runs to report **what changed**:

```json
{
  "lastRun": "2026-02-05T10:30:00Z",
  "issueSnapshots": {
    "12345": {
      "lastSeenAt": "2026-02-05T...",
      "category": "trending",
      "priorityScore": 82
    }
  },
  "analysisResults": {
    "12345": {
      "success": true,
      "analyzedAt": "2026-02-05T...",
      "data": { ... }
    }
  }
}
```

**Delta Report Shows**:
- Issues with **new activity** since last run
- **Newly analyzed** vs **cached** results
- Category **changes** (e.g., was needs-info, now ready-for-fix)
- **Analysis failures** that need retry

## Output Format

### Executive Summary (`summary.md`)

```markdown
# Issue Triage Summary - 2026-02-05

**Run Type**: Weekly | **Issues Analyzed**: 47 | **Since**: 2026-01-29

## Action Required by Category

| Category | Count | Top Priority |
|----------|-------|--------------|
| 🔥 Trending | 3 | #12345 (12 new comments) |
| 🏷️ Needs-Label | 5 | #12346 (suggest: FancyZones) |
| ✅ Ready-for-Fix | 2 | #12347 (score: 85/100) |
| ❓ Needs-Info | 8 | #12348 (missing repro) |
| 💬 Needs-Clarification | 4 | #12349 (question about feature) |
| ✔️ Closeable | 6 | #12350 (fixed in v0.99) |

## Quick Actions

- [ ] Review #12345 - trending with negative sentiment
- [ ] Label #12346 as Product-FancyZones
- [ ] Assign #12347 to @contributor
- [ ] Post clarification on #12348 (draft ready)
- [ ] Close #12350 with release note link
```

## State Schema

See [State Management](./references/step1-state-management.md) for full schema.

```json
{
  "version": "1.0",
  "lastRun": "2026-02-05T10:30:00Z",
  "lastRunType": "weekly",
  "issueSnapshots": {
    "12345": {
      "number": 12345,
      "title": "FancyZones: Window snapping issue",
      "state": "open",
      "lastSeenAt": "2026-02-05T...",
      "category": "trending",
      "priorityScore": 82
    }
  },
  "analysisResults": {
    "12345": {
      "success": true,
      "analyzedAt": "2026-02-05T10:30:00Z",
      "data": {
        "issueNumber": 12345,
        "category": "trending",
        "categoryReason": "8 new comments, heated discussion",
        "priorityScore": 82,
        "suggestedAction": "Review conversation urgently",
        "draftReply": "...",
        "clarityScore": 75,
        "feasibilityScore": 80
      }
    }
  },
  "statistics": {
    "totalRunCount": 12,
    "issuesAnalyzed": 234
  }
}
```

## Cache Invalidation Rules

Analysis results are **cached** and reused when:
- Issue has **no new activity** since last analysis
- Analysis is **less than 7 days old**
- `-Force` flag is **not** specified

Re-analysis triggers:
- New comments on the issue
- Issue state changed
- Cache older than 7 days
- Explicit `-Force` flag

## Integration with review-issue Prompt

For issues in **Ready-for-Fix** or complex **Needs-Info** categories, this skill automatically invokes the [review-issue prompt](../../prompts/review-issue.prompt.md) to generate:
- Detailed `overview.md` with scoring
- `implementation-plan.md` for ready issues

Results are cached in `issue-cache/XXXXX/` and reused across runs.

## Troubleshooting

| Issue | Solution |
|-------|----------|
| No `triage-state.json` | First run—will create initial state |
| PowerShell version error | Requires PowerShell 7+ for `-Parallel` |
| Copilot CLI not found | Install: `gh extension install github/gh-copilot` |
| Too many timeouts | Increase `-TimeoutMinutes` or reduce `-MaxParallel` |
| High failure rate | Check `issue-cache/*/error.log` for details |
| Stale cache | Use `-Force` to re-analyze all issues |
| gh rate limit | Wait or reduce `-MaxParallel` |
| Empty analysis results | Check Copilot CLI auth: `gh auth status` |

## Conventions

- **Preserve history**: Archive each run to `history/YYYY-MM-DD/`
- **Draft replies**: Always human-review before posting
- **Label suggestions**: Confidence threshold 70% for auto-suggest
- **Closed issues**: Track for 30 days after close for late comments
