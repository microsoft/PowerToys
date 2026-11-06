# File Locksmith — PowerToys release checklist

> Source: split from `release-checklist-annotated.md` (generated 2026-06-06). One module per file.

## Legend

Each item is annotated with an admin-requirement tag:

**Admin requirement**:
- `[ADMIN: NO]` - runnable from a standard (non-elevated) shell
- `[ADMIN: YES]` - requires elevated session (writes to HKLM, %WinDir%\System32, MSI install, GPO templates, etc.)
- `[ADMIN: COND]` - conditional - the basic case is non-admin but specific sub-cases require admin (e.g. "test with elevated target app", "Restart as admin" variants)

---

## File Locksmith (10 items)

- [ ] **[ADMIN: COND]** (L641) **Right-click the PowerToys installer executable** and select `Unlock with File Locksmith`. Confirm the entry is present and launches the File Locksmith window (titled **File Locksmith**). The window lists the processes using the file — **two entries** (each showing the process name + an **End task** button), because the installer starts two processes.
- [ ] **[ADMIN: COND]** (L642) Click **End task** on each listed installer process in the File Locksmith UI. Confirm each **End task** terminates that process (the installer window closes) and its **row is removed** from the list.
- [ ] **[ADMIN: COND]** (L643) **Start the installer executable again**, then click the **Reload** button (top-right refresh) in the File Locksmith UI. Confirm the newly-started process(es) using the file are **rediscovered and added** to the list.
- [ ] **[ADMIN: COND]** (L644) **Close the installer window** (let its process exit) **without** pressing Reload. Confirm File Locksmith **auto-delists** the exited process(es) within ~1–2 s (no manual refresh), and close the window.
- [ ] **[ADMIN: COND]** (L646) **Right-click the directory** containing the executable and select `Unlock with File Locksmith`. Confirm the entry appears and the UI lists the locking process(es) found **recursively** inside that directory tree.
- [ ] **[ADMIN: COND]** (L647) **Right-click the drive** where the executable is located and select `Unlock with File Locksmith`. Confirm the entry appears and the UI lists the locking process(es) on that volume. (You can close the PowerToys installer now.)
- [ ] **[ADMIN: COND]** (L649) With File Locksmith launched **non-elevated**, **right-click "Program Files"** (the folder holding the elevated PowerToys runner) and select `Unlock with File Locksmith`. Confirm **`PowerToys.exe` does NOT appear** in the list — a non-elevated File Locksmith cannot read the higher-integrity (elevated) runner process.
- [ ] **[ADMIN: YES]** (L650) In that same non-elevated File Locksmith window, click **Restart as an administrator** (shield button, shown only when non-elevated) and approve the UAC prompt. Confirm that after relaunching **elevated** (title becomes **Administrator: File Locksmith**), **`PowerToys.exe` now appears** in the list — the elevated File Locksmith can see higher-integrity processes.
- [ ] **[ADMIN: YES]** (L651) **Right-click the drive where Windows is installed** and select `Unlock with File Locksmith`. With the (large) process list shown, **scroll all the way down and back up** and confirm File Locksmith **does not crash** while rendering all those entries. **Repeat** after clicking **Restart as an administrator**, and confirm no crash in the elevated view either.
- [ ] **[ADMIN: COND]** (L652) **Disable** File Locksmith in PowerToys Settings. Right-click a file and confirm `Unlock with File Locksmith` is **absent** from **both** the Win11 tier-1 (modern) menu **and** the classic "Show more options" (`#32768`) menu, while sibling PowerToys entries (e.g. `Rename with PowerRename`) **remain present** — proving the menu still renders and only File Locksmith was gated out (not a render failure). **Re-enable** and confirm the entry **reappears** in both menus.


