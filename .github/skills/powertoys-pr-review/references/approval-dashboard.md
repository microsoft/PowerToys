# Approval Dashboard

Use the dashboard for two or more PRs or whenever the user asks for an interactive approval surface. It is both a live batch-status tracker and the mandatory Step 10 approval gate.

The dashboard never posts to GitHub. It validates public payloads, records the user's choices, binds them to the exact review-data hash, and hands them to `Publish-ApprovedReview.ps1`.

## Safety boundaries

- `publicPayload` is the only author-facing source.
- `internalEvidence` is display-only and never enters decisions or posting plans.
- Public text must not mention a fork repository, fork PR, worktree, private thread, convergence loop, or private validation provenance.
- Inline items require one non-empty apply-ready `suggestion` block and an exact current RIGHT-side diff range.
- Companion items contain readable review-body guidance for work that cannot use an apply button.
- The dashboard has no approve action.
- Submission is blocked while any PR is queued/in progress or validation fails.

## Parallel batch flow

Follow [batch-parallel.md](./batch-parallel.md): review PRs concurrently in isolated worktrees, have each worker produce a per-PR result, and let one coordinator update `review-data.json` atomically.

1. Create schema-version-2 `review-data.json` with every PR in `phase: "queued"`.
2. Launch:
   ```powershell
   ./scripts/Show-ReviewDashboard.ps1 `
     -DataPath <path>\review-data.json `
     -Port 8787 `
     -RepoDir <path-to-PowerToys>
   ```
3. Update each PR through `queued`, `mirroring`, `building`, `reviewing`, `drafting`, then `ready`, `held`, or `error`.
4. Before `ready`, pin `headSha`, capture `snapshot`, populate `publicPayload`, and run:
   ```powershell
   ./scripts/Test-ReviewData.ps1 -DataPath <path>\review-data.json -AllowIncomplete -CheckGitHub
   ```
5. Tell the user the dashboard URL and stop. The user selects public items and submits decisions.
6. Resume with `pr-review: actions ready`, then run:
   ```powershell
   ./scripts/Publish-ApprovedReview.ps1 `
     -DataPath <path>\review-data.json `
     -DecisionsPath <path>\review-decisions.json
   ```

The page polls `/status` every few seconds. The coordinator is the only writer of `review-data.json`; write a temporary file and atomically replace the real file so the poller never reads partial JSON.

## `review-data.json` schema

```jsonc
{
  "schemaVersion": 2,
  "repository": "microsoft/PowerToys",
  "generatedAt": "2026-08-06T08:00:00Z",
  "phase": "reviewing",
  "prs": [
    {
      "number": 43741,
      "url": "https://github.com/microsoft/PowerToys/pull/43741",
      "title": "AdvancedPaste: Paste As Keystrokes",
      "author": "tonur",
      "phase": "ready",
      "loop": 4,
      "waitingOn": "",
      "assoc": "FIRST_TIME_CONTRIBUTOR",
      "firstTimer": true,
      "draft": false,
      "headSha": "0123456789abcdef0123456789abcdef01234567",
      "snapshot": {
        "updatedAt": "2026-08-06T07:55:00Z",
        "issueCommentCount": 5,
        "reviewCommentCount": 12,
        "reviewCount": 4
      },
      "disposition": "Request changes",
      "phase0Note": "CLA on file. Demo requested. CI green.",
      "publicPayload": {
        "contextBody": "Thanks for the contribution. Please address the correctness issue below.",
        "items": [
          {
            "id": "stable-value",
            "kind": "inline",
            "severity": "high",
            "title": "Preserve the persisted value",
            "path": "src/.../PasteFormats.cs",
            "startLine": 40,
            "line": 40,
            "side": "RIGHT",
            "body": "### Preserve the persisted value\n\n**Severity:** `high`\n\nThe new member shifts existing values. Preserve the established assignment so upgrades retain the same meaning.\n\n```suggestion\n    Existing = 1,\n```"
          },
          {
            "id": "cross-file-tests",
            "kind": "companion",
            "severity": "medium",
            "title": "Add cross-file regression coverage",
            "body": "### Add cross-file regression coverage\n\n**Severity:** `medium`\n\nPlease add regression coverage for the related files outside this PR diff. GitHub cannot offer an apply button because those files are not changed here."
          }
        ]
      },
      "internalEvidence": {
        "status": "reviewed-pending-approval",
        "worktree": "C:\\PowerToys-review-43741",
        "validationSummary": "Debug build and targeted tests passed",
        "privateReviewUrl": "internal URL"
      },
      "testInstructions": "Enable Advanced Paste, bind the hotkey, and verify multiline input."
    }
  ]
}
```

### Public item rules

| `kind` | Requirements | Published as |
| --- | --- | --- |
| `inline` | Canonical `body`; exactly one non-empty `suggestion` fence; `path`, `line`, optional `startLine`; `side: RIGHT`; range in one current diff hunk | Inline review comment |
| `companion` | Canonical readable `body`; no suggestion fence; no path required | Review body section |

Do not use separate `body` and `fix` fields. Do not place status, links, build evidence, or private review provenance in a public item.

The validator rejects:

- placeholders and empty/multiple suggestion blocks;
- invalid or stale line ranges;
- stale head SHAs;
- fork/internal references in public text;
- PowerShell serialization/interpolation artifacts;
- missing activity snapshots; and
- duplicate PR or item IDs.

## `review-decisions.json` schema

The dashboard writes:

```jsonc
{
  "schemaVersion": 2,
  "reviewDataHash": "<sha256 of the exact review-data.json>",
  "submittedAt": "2026-08-06T08:10:00Z",
  "launch": true,
  "prs": [
    {
      "number": 43741,
      "headSha": "0123456789abcdef0123456789abcdef01234567",
      "action": "request-changes",
      "postContext": true,
      "contextBody": "Thanks for the contribution. Please address the correctness issue below.",
      "items": {
        "stable-value": "post",
        "cross-file-tests": "hold"
      },
      "instructions": ""
    }
  ]
}
```

| `action` | Behavior |
| --- | --- |
| `comment` | Stage selected items as a pending COMMENT review, verify, then submit |
| `request-changes` | Stage selected items as a pending REQUEST_CHANGES review, verify, then submit |
| `hold` | Post nothing |
| `close` | Manual maintainer action; safe publisher refuses it |
| `custom` | Manual action; safe publisher refuses it |

There is no approve action. The skill never approves an upstream PR.

## Submit-time validation

`Show-ReviewDashboard.ps1` re-reads the latest data and checks:

1. every PR is terminal (`ready`, `held`, or `error`);
2. every ready public payload passes structural validation;
3. pinned heads and inline ranges still match GitHub;
4. decisions reference the exact current data hash and head SHA;
5. every selected ID exists and is `post` or `hold`; and
6. edited context text contains no forbidden internal references.

Invalid decisions are not written.

## Launch on submit

When `-RepoDir` is provided, the page can open a supervised Copilot session with:

```powershell
copilot -C <RepoDir> -i "pr-review: actions ready (decisions file: <DecisionsPath>)"
```

This is not unattended posting. The resumed session must still use `Publish-ApprovedReview.ps1`, which re-checks freshness and verifies the pending review before submission.
