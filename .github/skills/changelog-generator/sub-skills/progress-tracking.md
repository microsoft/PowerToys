# Progress Tracking for Large Changelogs

This sub-skill provides the checkpoint mechanism for processing many commits without losing progress.

## When to Use Progress Tracking

- Processing 50+ commits
- Long-running changelog generation
- Risk of context overflow in AI conversations

## Checkpoint Mechanism

1. **Before starting**: Create a progress tracking file `release-notes-progress.md`
2. **After each batch**: Append processed results to `release-change-note-draft.md`
3. **Track position**: Record the last processed commit SHA in `release-notes-progress.md`

## Progress File Template

Create `release-notes-progress.md`:

```markdown
# Release Notes Generation Progress

## Configuration
- Start Tag: v0.96.0
- End Tag: v0.96.1
- Total Commits: 127
- Batch Size: 20

## Progress Tracker
| Batch | Status | Last SHA | PRs Processed |
|-------|--------|----------|---------------|
| 1 (1-20) | ✅ Done | abc1234 | #1001, #1002, #1003... |
| 2 (21-40) | ✅ Done | def5678 | #1004, #1005... |
| 3 (41-60) | 🔄 In Progress | ghi9012 | #1006... |
| 4 (61-80) | ⏳ Pending | - | - |
| 5 (81-100) | ⏳ Pending | - | - |
| 6 (101-120) | ⏳ Pending | - | - |
| 7 (121-127) | ⏳ Pending | - | - |

## Processed PRs (deduplication list)
#1001, #1002, #1003, #1004, #1005, #1006

## Last Checkpoint
- Timestamp: 2025-01-07 10:30:00
- Last processed commit: ghi9012
- Next commit to process: jkl3456
```

## Batch Processing Workflow

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Get total commit count and create progress file          │
└─────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. Filter: Skip commits already in start tag                │
│    - Check if commit is ancestor of start tag               │
│    - Skip cherry-picks or backports already released        │
└─────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. Process batch of 15-20 commits                           │
│    - Fetch commit details                                   │
│    - Get associated PRs                                     │
│    - Generate changelog entries                             │
└─────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. CHECKPOINT: Save progress                                │
│    - Append entries to release-change-note-draft.md         │
│    - Update release-notes-progress.md with last SHA         │
│    - Record processed PR numbers                            │
└─────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ 5. Check: More commits remaining?                           │
│    YES → Go to step 3 with next batch                       │
│    NO  → Go to step 6                                       │
└─────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ 6. Final merge and formatting                               │
│    - Combine all batches                                    │
│    - Deduplicate by PR number                               │
│    - Sort by module alphabetically                          │
│    - Add highlights section                                 │
└─────────────────────────────────────────────────────────────┘
```

## Resuming from Checkpoint

If interrupted, read `release-notes-progress.md` to find:
1. Which batch was last completed
2. The SHA of the last processed commit
3. Which PRs have already been processed (for deduplication)

Then continue from the next unprocessed commit:

```powershell
# Find where you left off
$lastSha = "abc1234"  # from progress file
$remainingShas = gh api repos/microsoft/PowerToys/compare/$lastSha...main --jq '.commits[].sha'
```

## Batch Size Recommendations

| Total Commits | Recommended Batch Size |
|---------------|------------------------|
| < 30 | Process all at once |
| 30-100 | 15-20 per batch |
| 100-300 | 20 per batch |
| 300+ | 25 per batch + parallel processing |

## Deduplication

Track processed PR numbers to avoid duplicates:
- Same PR can appear multiple times (multiple commits)
- Cherry-picks may reference same PR
- Always check `Processed PRs` list before generating entry
