# Preview release scenario

Convert one successful PowerToys ADO release-candidate build into a complete GitHub draft prerelease. Run autonomously to either a verified draft or a terminal failure report. Never ask for a decision after starting and never publish a release.

## Inputs

- One ADO build URL or numeric build ID.
- Optional engineering flags: dry run and output directory.

Use:

```text
Generated Files/ReleaseNotes/preview-<buildId>/
```

for every local artifact.

## Candidate eligibility

During release preparation, preview builds may come from either `main` or `stable`; both are official supported patterns. Any successful build from trusted release definition `76541` on either branch is eligible regardless of its resolved intent, channel, or `shouldPublishPreview` value.

`get-release-build-metadata.ps1` first reads the pipeline-published `release-metadata.json` and falls back to immutable versioning logs for older builds. Preserve the original metadata as audit evidence, but do not use release intent or channel as eligibility gates. The requested workflow determines that GitHub receives a draft prerelease.

Reject builds from other branches, failed or incomplete builds, non-release definitions, unresolved versions or commits, and missing or invalid assets.

## Tag and artifact versions

Use the first three numeric components of the resolved build version for the GitHub tag, prefixed with `v`: `0.102.2991.0` becomes `v0.102.2991`. The metadata script returns this as `$context.tag`. Do not construct the tag from the full `$context.version` or add a `-preview` suffix.

Keep the four-component `$context.version` for ADO artifact lookup, installer and GPO filenames, hashes, and build audit metadata. Installer download URLs use the three-component tag and the unchanged four-component filename. Older PowerToys updaters reject four-component release tags; this format remains parseable if the release is later promoted through the separate release-management process. This workflow still creates only draft prereleases and never sets a release as Latest.

## Release title

Use the requested GitHub release type and the immutable build's source branch:

| Release type | Build source | Title |
| --- | --- | --- |
| Stable release (separate workflow) | `stable` | `Release <tag>` |
| Prerelease | `stable` | `Release preview <tag>` |
| Prerelease | `main` | `Development preview <tag>` |

For example, a preview of build `0.102.2991.0` from `stable` is `Release preview v0.102.2991`. Pass `$context.sourceBranch` explicitly to creation and verification. Do not infer it from a local checkout or `TargetCommit`, and do not use the build's intent/channel to override the requested prerelease type. If release management later promotes the preview to a stable release, change its title to `Release <tag>` as part of that separate operation.

## Workflow

1. Resolve and validate build metadata:

   ```powershell
   $context = .\.github\skills\release-note-generation\scripts\get-release-build-metadata.ps1 `
     -Build '<ADO URL or ID>' `
     -OutputPath '<run directory>\release-context.json'
   ```

2. Select the previous published release:

   ```powershell
   $baseline = .\.github\skills\release-note-generation\scripts\get-previous-published-release.ps1 `
     -TargetTag $context.tag `
     -QueuedAt $context.queuedAt `
     -OutputPath '<run directory>\previous-release.json'
   ```

3. Fetch the candidate source commit, baseline tag, and required history. Calculate the semantic delta:

   ```powershell
   $delta = .\.github\skills\release-note-generation\scripts\get-preview-release-delta.ps1 `
     -PreviousCommit $baseline.sourceCommit `
     -TargetCommit $context.sourceCommit `
     -OutputDirectory '<run directory>'
   ```

   Follow [preview delta resolution](../preview-delta-resolution.md). A same-lineage rollback is fatal. Added, removed, and unattributed changes are all review evidence and must not be silently dropped.

4. Collect normalized metadata for added PRs:

   First create `<run directory>\MemberList.md` from the **PowerToys core team** section in [`COMMUNITY.md`](../../../../../COMMUNITY.md), following the exact username format in [Step 1.0.1](../step1-collection.md#101-generate-memberlistmd-required). Then run:

   ```powershell
   .\.github\skills\release-note-generation\scripts\collect-pr-metadata.ps1 `
     -DeltaPath '<run directory>\delta-prs.json' `
     -OutputDirectory '<run directory>' `
     -MemberListPath '<run directory>\MemberList.md'
   ```

5. Generate summaries and compose `release-notes.md` using the existing label, contributor-attribution, grouping, and formatting conventions. Preview-specific rules:

   - Do not assign milestones or change labels.
   - Put unlabeled PRs under `General`.
   - Use conservative title-based wording when summary confidence is low.
   - Start the body with a short public introduction. Do not repeat the release name as a heading: GitHub and the in-app What's new page already display it. This overrides the title-heading example in the shared summarization instructions.
   - Place `Installer Hashes` immediately after the introduction, before `Highlights` and all change sections. Use level-two headings for sections, not a level-one release title.
   - Include removed PRs under `Differences from the previous preview`.
   - Include unattributed commits under `Changes needing final review`.
   - Enclose the generated body in the managed markers documented in [preview draft safety](../preview-draft-safety.md).

6. Create `release-manifest.json` from the build, baseline, and delta outputs. Keep this manifest in the local audit package; never upload it as a GitHub release asset:

   ```powershell
   .\.github\skills\release-note-generation\scripts\new-preview-release-manifest.ps1 `
     -ContextPath '<run directory>\release-context.json' `
     -PreviousReleasePath '<run directory>\previous-release.json' `
     -DeltaDirectory '<run directory>' `
     -OutputPath '<run directory>\release-manifest.json'
   ```

7. Download and validate all release assets:

   ```powershell
   .\.github\skills\release-note-generation\scripts\prepare-release-assets.ps1 `
     -BuildId $context.buildId `
     -Version $context.version `
     -ReleaseTag $context.tag `
     -DestinationFolder '<run directory>\assets'
   ```

   Keep the generated `assets-manifest.json` in the local audit package. Do not upload it as a GitHub release asset.

8. In dry-run mode, write the complete local review report and stop without contacting GitHub:

   ```powershell
   .\.github\skills\release-note-generation\scripts\verify-draft-preview-release.ps1 `
     -Tag $context.tag `
     -SourceBranch $context.sourceBranch `
     -TargetCommit $context.sourceCommit `
     -AssetsDirectory '<run directory>\assets' `
     -BodyPath '<run directory>\release-notes.md' `
     -ContextPath '<run directory>\release-context.json' `
     -PreviousReleasePath '<run directory>\previous-release.json' `
     -DeltaDirectory '<run directory>' `
     -OutputPath '<run directory>\final-review.md' `
     -DryRun
   ```

9. Otherwise, create or update the draft prerelease:

   ```powershell
   .\.github\skills\release-note-generation\scripts\upsert-draft-preview-release.ps1 `
     -Tag $context.tag `
     -SourceBranch $context.sourceBranch `
     -TargetCommit $context.sourceCommit `
     -BodyPath '<run directory>\release-notes.md' `
     -AssetsDirectory '<run directory>\assets'
   ```

10. Verify the resulting draft:

    ```powershell
    .\.github\skills\release-note-generation\scripts\verify-draft-preview-release.ps1 `
      -Tag $context.tag `
      -SourceBranch $context.sourceBranch `
      -TargetCommit $context.sourceCommit `
      -AssetsDirectory '<run directory>\assets' `
      -ContextPath '<run directory>\release-context.json' `
      -PreviousReleasePath '<run directory>\previous-release.json' `
      -DeltaDirectory '<run directory>' `
      -OutputPath '<run directory>\final-review.md'
    ```

Return the draft URL and the final review package described in [preview reporting](../preview-reporting.md).
