# Preview draft safety

The preview workflow may create or update GitHub drafts only.

## Required identity

```text
Tag:        v<first three numeric components of resolved version>
Title:      Release preview <tag> (stable) / Development preview <tag> (main)
Target:     exact ADO source commit
Draft:      true
Prerelease: true
```

Never use a branch name as the release target and never expose a publish parameter.

For build `0.102.2991.0`, use tag `v0.102.2991`. Select `Release preview v0.102.2991` for source branch `refs/heads/stable`, or `Development preview v0.102.2991` for `refs/heads/main`. Reserve `Release <tag>` for actual stable releases, never for this draft-prerelease workflow, even if the build originally had stable intent/channel. Keep four-component versions in artifact filenames and audit metadata. Use `$context.tag` throughout baseline selection, draft creation, verification, and installer download links. Reject four-component or suffixed tags rather than silently changing an explicitly supplied tag.

Do not rename or delete previously published four-component releases or tags. Historical releases remain valid baselines; the three-component rule applies to the new draft's identity.

## Managed body

Wrap generated release content with:

```markdown
<!-- BEGIN POWERTOYS PREVIEW AGENT -->
PowerToys <resolved version> is a development preview ...

## Installer Hashes
...

## Highlights
...
<!-- END POWERTOYS PREVIEW AGENT -->
```

Keep the release title in GitHub's title field only. Do not add a level-one heading to the managed body: GitHub and the in-app What's new page already render the release name above it. The draft creation and verification scripts reject level-one headings rather than silently rewriting generated content.

On rerun, replace only this region. Preserve human-authored text before and after it.

## Idempotency

- Create a draft when the tag is unused.
- Update an existing draft for the same tag.
- Stop if a published release owns the tag.
- Replace only expected generated assets.
- Keep `release-manifest.json` and `assets-manifest.json` local and remove any stale uploaded copies on rerun.
- Validate all local files before creating the draft.
- After every write, assert `draft=true` and `prerelease=true`.

If an upload or final verification fails, leave the release as a draft and report the incomplete state. A rerun must be able to replace missing or mismatched assets.
