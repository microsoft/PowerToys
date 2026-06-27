# New+ — PowerToys release checklist

> Source: split from `release-checklist-annotated.md` (generated 2026-06-06). One module per file.

## Legend

Each item is annotated with two metadata tags:

**Admin requirement**:
- `[ADMIN: NO]` - runnable from a standard (non-elevated) shell
- `[ADMIN: YES]` - requires elevated session (writes to HKLM, %WinDir%\System32, MSI install, GPO templates, etc.)
- `[ADMIN: COND]` - conditional - the basic case is non-admin but specific sub-cases require admin (e.g. "test with elevated target app", "Restart as admin" variants)

**Clarity**:
- (no marker) - clear, has explicit assert
- `[CLARITY: VAGUE-NO-STEPS]` - original wording is just a module/feature name without procedural steps
- `[CLARITY: VAGUE-NO-ASSERT]` - original wording describes an action but does not state the expected outcome
- `[CLARITY: VAGUE-AMBIGUOUS]` - original wording uses vague verbs like "works" without a measurable outcome
- `[REWRITTEN]` - original wording was vague; this checklist has rewritten the description to be concrete. Original wording preserved in italics below the item.

---

## New+ (9 items)

- [ ] **[ADMIN: NO]** (L969) Verify NewPlus menu is in Explorer context menu. (Windows 11 tier 1 context menu only. May need Explorer restart.)
- [ ] **[ADMIN: NO]** (L971) Verify NewPlus menu is not in Explorer context menu.
- [ ] **[ADMIN: NO]** (L973) Verify the folder is created and empty.
- [ ] **[ADMIN: NO]** (L974) Copy a file to the templates folder, verify it's added to the New+ context menu and that if you select it the file is created.
- [ ] **[ADMIN: NO]** (L975) Copy a folder with files inside to the templates folder, verify it's added to the New+ context menu and that if you select it the folder and files inside are created.
- [ ] **[ADMIN: NO]** (L976) Delete all files and folders from inside the templates folder. Verify that no templates are available in the context menu.
- [ ] **[ADMIN: NO]** (L977) Disable and re-Enable New+ while the templates folder is still empty. Verify the default templates were copied over and are available in the context menu.
- [ ] **[ADMIN: NO]** (L979) Test the "Hide template filename extension" option in Settings.
- [ ] **[ADMIN: NO]** (L980) Test the "Hide template filename starting digits, spaces and dots" option in Settings.

