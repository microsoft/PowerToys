# Peek — PowerToys release checklist

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

## Peek (18 items)

- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L697) Image
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L698) Text or dev file
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L699) Markdown file
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L700) PDF
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L701) HTML
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L702) Archive files (.zip, .tar, .rar)
- [ ] **[ADMIN: NO]** (L703) Any other not mentioned file (.exe for example) to verify the unsupported file view is shown
- [ ] **[ADMIN: NO]** (L706) Pin the window, switch between images of different size, verify the window stays at the same place and the same size.
- [ ] **[ADMIN: NO]** (L707) Pin the window, close and reopen Peek, verify the new window is opened at the same place and the same size as before.
- [ ] **[ADMIN: NO]** (L708) Unpin the window, switch to a different file, verify the window is moved to the default place.
- [ ] **[ADMIN: NO]** (L709) Unpin the window, close and reopen Peek, verify the new window is opened on the default place.
- [ ] **[ADMIN: NO]** (L712) By clicking a button.
- [ ] **[ADMIN: NO]** (L713) By pressing enter.
- [ ] **[ADMIN: NO]** (L716) Can use peek command to peek files
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-NO-STEPS]** (L717) Peek can work without problem when a peek session is on
- [ ] **[ADMIN: NO]** (L719) Switch between files in the folder using `LeftArrow` and `RightArrow`, verify you can switch between all files in the folder.
- [ ] **[ADMIN: NO]** (L720) Open multiple files, verify you can switch only between selected files.
- [ ] **[ADMIN: NO] [CLARITY: VAGUE-AMBIGUOUS]** (L721) Change the shortcut, verify the new one works.

