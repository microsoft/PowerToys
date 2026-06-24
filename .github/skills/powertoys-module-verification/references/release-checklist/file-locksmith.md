# File Locksmith — PowerToys release checklist

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

## File Locksmith (10 items)

- [ ] **[ADMIN: COND]** (L641) Right-click the executable file, select "Unlock with File Locksmith" and verify it shows up. (2 entries will show, since the installer starts two processes)
- [ ] **[ADMIN: COND]** (L642) End the tasks in File Locksmith UI and verify that closes the installer.
- [ ] **[ADMIN: COND]** (L643) Start the installer executable again and press the Refresh button in File Locksmith UI. It should find new processes using the files.
- [ ] **[ADMIN: COND]** (L644) Close the installer window and verify the processes are delisted from the File Locksmith UI. Close the window
- [ ] **[ADMIN: COND]** (L646) Right click the directory where the executable is located, select "Unlock with File Locksmith" and verify it shows up.
- [ ] **[ADMIN: COND]** (L647) Right click the drive where the executable is located, select "Unlock with File Locksmith" and verify it shows up. You can close the PowerToys installer now.
- [ ] **[ADMIN: COND]** (L649) Right click "Program Files", select "Unlock with File Locksmith" and verify "PowerToys.exe" doesn't show up.
- [ ] **[ADMIN: YES]** (L650) Press the File Locksmith "Restart as an administrator" button and verify "PowerToys.exe" shows up.
- [ ] **[ADMIN: YES]** (L651) Right-click the drive where Windows is installed, select "Unlock with File Locksmith" and scroll down and up, verify File Locksmith doesn't crash with all those entries being shown. Repeat after clicking the File Locksmith "Restart as an administrator" button.
- [ ] **[ADMIN: COND]** (L652) Disable File Locksmith in Settings and verify the context menu entry no longer appears.

