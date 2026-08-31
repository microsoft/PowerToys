# Release scenario router

Select exactly one workflow before running release-note scripts.

| Scenario | Required input | Content boundary | Human interaction |
| --- | --- | --- | --- |
| Stable release | Version or milestone | Existing stable release range | Existing stable workflow |
| Preview release | ADO build URL or build ID | Previous published release to the build's exact source commit | Final draft review only |

Use [stable-release.md](./stable-release.md) for the existing milestone workflow.

Use [preview-release.md](./preview-release.md) when the requested output is a GitHub draft prerelease. Preview mode derives the version, branch, previous tag, and target commit from immutable build and release metadata. Do not run milestone assignment or modify PR labels in preview mode.
