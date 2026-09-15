# Preview draft safety

The preview workflow may create or update GitHub drafts only.

## Required identity

```text
Tag:        v<resolved version>
Title:      Preview v<resolved version>
Target:     exact ADO source commit
Draft:      true
Prerelease: true
```

Never use a branch name as the release target and never expose a publish parameter.

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
