# Image Resizer — PowerToys release checklist

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

## Image Resizer (18 items)

- [ ] **[ADMIN: NO]** (L309) Disable the Image Resizer and check that `Resize with Image Resizer` is absent in the context menu
- [ ] **[ADMIN: NO]** (L310) Enable the Image Resizer and check that `Resize with Image Resizer` is present in the context menu (both Win11 modern and old menus)
- [ ] **[ADMIN: NO]** (L311) Remove one image size and add a custom image size. Open the Image Resize window from the context menu and verify changes are populated
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L312) Resize one image
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L313) Resize multiple images
- [ ] **[ADMIN: NO]** (L314) Open image resizer to resize a .gif and verify "Gif files with animations may not be correctly resized." warning appears
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L316) Resize images with Fill option
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L317) Resize images with Fit option
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L318) Resize images with Stretch option
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L320) Resize using dimension Centimeters
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L321) Resize using dimension Inches
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L322) Resize using dimension Percents
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L323) Resize using dimension Pixels
- [ ] **[ADMIN: NO]** (L325) Change Filename format to %1 - %2 - %3 - %4 - %5 - %6 and verify applied
- [ ] **[ADMIN: NO]** (L326) Check Use original date modified and verify modified date not changed for resized
- [ ] **[ADMIN: NO]** (L327) Check Make pictures smaller but not larger and verify smaller pictures not resized
- [ ] **[ADMIN: NO]** (L328) Check Resize the original pictures (don't create copies) and verify original is resized
- [ ] **[ADMIN: NO]** (L329) Uncheck Ignore the orientation and verify swapped W/H actually resizes if W!=H

