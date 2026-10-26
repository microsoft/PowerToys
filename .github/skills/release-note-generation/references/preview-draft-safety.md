# Preview draft safety

The preview workflow may create or update GitHub drafts only.

## Required identity

```text
Tag:        v<first three numeric components of resolved version>
Title:      Preview <tag>
Target:     exact ADO source commit
Draft:      true
Prerelease: true
```

Never use a branch name as the release target and never expose a publish parameter.

For build `0.102.2991.0`, use tag `v0.102.2991` and title `Preview v0.102.2991`. Keep four-component versions in artifact filenames and audit metadata. Use `$context.tag` throughout baseline selection, draft creation, verification, and installer download links. Reject four-component or suffixed tags rather than silently changing an explicitly supplied tag.

Do not rename or delete previously published four-component releases or tags. Historical releases remain valid baselines; the three-component rule applies to the new draft's identity.

## Managed body

Wrap generated release content with:

```markdown
<!-- BEGIN POWERTOYS PREVIEW AGENT -->
...
<!-- END POWERTOYS PREVIEW AGENT -->
```

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
