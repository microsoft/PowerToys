# Preview release reporting

Write `final-review.md` and return the same key information to the user:

- Draft release URL, or explicit `Not created (dry run)` status for a local-only run.
- ADO build URL and build ID.
- Version, source branch, exact source commit, intent, and channel.
- Previous release tag and source commit.
- Delta mode and merge base when applicable.
- Added PR count and list.
- Removed PR count and list.
- Unattributed commit count and list.
- Uploaded asset inventory.
- Installer hash and signature results.
- Low-confidence note sections.
- Final human publication checklist.

Use explicit PASS, WARNING, or FAILURE wording. A successful live run ends with a complete draft ready for human review. A successful dry run reports a complete local package without implying that a GitHub draft exists. A failed run identifies the terminal safety gate and must not imply that a usable draft exists.
