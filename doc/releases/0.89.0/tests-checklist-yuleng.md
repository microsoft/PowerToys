# Release Checklist - v0.XX

## Leading to release

- [ ] Readme Update created
    - [ ] Go through current release's [project board](https://github.com/microsoft/PowerToys/projects) and note any completed high-level feature work (recognizing any community contributions)
    - [ ] Go through [PRs merged since the last release](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+merged%3A%3E2021-03-01) and note any significant feature work & improvements (recognizing any community contributions)
        - The above link queries for results since March 1st, 2021. Update to appropriate start date for desired release.
    - [ ] Go through all [open issues with the "Resolution-Fix-Committed" tags](https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+is%3Aopen+label%3AResolution-Fix-Committed+) and note any significant bug fixes/improvements (recognizing any community contributions)
    - [ ] Draft Readme based on notes from above
    - [ ] Open PR for readme the week before release so engineering team and community can provide feedback
    - [ ] Merge into master branch after latest release goes live
- [ ] Microsoft Docs Update created
# TODO
- [ ] Release template updated with any new features / updates for testing

## Testing

see [Checklist template for testing](tests-checklist-template.md)

## Staging release

- [ ] Release template updated with any new features / updates for testing
- [ ] Create Release and base off Readme Update PR
- [ ] Upload exe
- [ ] Upload symbols
- [ ] Create YAML for [winget-pkgs](https://github.com/microsoft/winget-pkgs)

## Releasing
- [ ] Push live
- [ ] Merge Readme PR live
- [ ] Merge Docs.Microsoft live
- [ ] Submit PR to [winget-pkgs](https://github.com/microsoft/winget-pkgs)

Based on [wiki](https://github.com/microsoft/PowerToys/wiki/Release-check-list)
