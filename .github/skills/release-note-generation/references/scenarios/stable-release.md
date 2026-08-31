# Stable release scenario

Use the existing release-note workflow for a milestone or stable release version:

1. Confirm the release version.
2. Follow [Step 1](../step1-collection.md) to collect PRs and manage milestones.
3. Follow [Step 2](../step2-labeling.md) to classify PRs.
4. Follow [Step 3](../step3-review-grouping.md) to produce PR summaries and grouped data.
5. Follow [Step 4](../step4-summarization.md) to compose final release notes.
6. Run [prepare-release-assets.ps1](../../scripts/prepare-release-assets.ps1) when release assets are required.

Do not apply the preview-specific baseline, branch-transition, draft-upsert, or managed-body rules to the stable workflow.
