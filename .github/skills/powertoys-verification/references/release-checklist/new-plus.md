# New+ — PowerToys release checklist

> Source: split from `release-checklist-annotated.md` (generated 2026-06-06); items rewritten to be
> definitive against the New+ sign-off runs (`verify-NewPlus-*`) and `../modules/new-plus.md`.
> One module per file.

## Legend

Each item is annotated with an admin-requirement tag:

**Admin requirement**:
- `[ADMIN: NO]` - runnable from a standard (non-elevated) shell
- `[ADMIN: YES]` - requires elevated session (writes to HKLM, %WinDir%\System32, MSI install, GPO templates, etc.)
- `[ADMIN: COND]` - conditional - the basic case is non-admin but specific sub-cases require admin (e.g. "test with elevated target app", "Restart as admin" variants)

> **Note:** New+ appears on the folder-**background** ("New") menu and **only** on the Win11 tier-1
> (modern) context menu — **not** the classic "Show more options" (`#32768`) menu, so do not assert
> both tiers as you would for PowerRename/Image Resizer. See `../modules/new-plus.md` for driving
> details (settings paths, default-template source, and the synthetic-menu flow).

---

## New+ (9 items)

- [ ] **[ADMIN: NO]** (L969) **Enable** New+ in PowerToys Settings. Right-click empty space inside a folder to open the Win11 tier-1 background ("New") menu and confirm the **`New+`** submenu is **present**; expanding it shows at least **`Open templates`**. (New+ is Win11 tier-1 background-menu only; may need an Explorer restart the first time. Corroborating signals: CLSID `{FF90D477-…}` registered under `HKCU:\Software\Classes\CLSID`, and the shell-ext log line `New+ context menu registered`.)
- [ ] **[ADMIN: NO]** (L971) **Disable** New+ via the Settings toggle. Right-click empty space in a folder and confirm **`New+`** is **absent** from the background menu, while the rest of the "New" menu still renders (proving only New+ was gated, not a render failure). Corroborating: the CLSID `{FF90D477-…}` is **unregistered** (`HKCU` key gone) and the log shows `New+ context menu unregistered` (the sparse package itself stays `Status Ok` — hidden dynamically). **Re-enable** and confirm `New+` reappears.
- [ ] **[ADMIN: NO]** (L973) **Delete the entire Templates folder** (`%LOCALAPPDATA%\Microsoft\PowerToys\NewPlus\Templates`), then open the New+ submenu (building the menu triggers the shell-ext to recreate the folder). Confirm the Templates folder is **recreated and empty** (0 items) and the submenu offers **only `Open templates`** (no template items). (This is the shell-ext `create_folder_if_not_exist` path — distinct from L977's default-templates copy, which only happens via the Settings-UI enable transition.)
- [ ] **[ADMIN: NO]** (L974) Copy a **file** template (e.g. `MyTemplate.txt`) into the Templates folder. Open the New+ submenu and confirm a matching item **appears** (caption `MyTemplate` with the extension hidden if *Hide extension* is on, else `MyTemplate.txt`). **Select** the item and confirm the file **`MyTemplate.txt` is created** in the current folder (copied via `SHFileOperation FO_COPY`) and enters inline rename mode.
- [ ] **[ADMIN: NO]** (L975) Copy a **folder-with-files** template into the Templates folder (e.g. `MyFolder\` containing `fileA.txt` and `sub\fileB.txt`). Open the New+ submenu and confirm **`MyFolder`** appears. **Select** it and confirm the folder is created **whole and recursively** in the current folder — i.e. `MyFolder\fileA.txt` **and** `MyFolder\sub\fileB.txt` all exist.
- [ ] **[ADMIN: NO]** (L976) **Empty** the Templates folder (delete all contents but keep the folder itself). Open the New+ submenu and confirm **no template items** are listed — only the always-present **`Open templates`** command remains.
- [ ] **[ADMIN: NO]** (L977) With the Templates folder still **empty** (from L976), **disable** and then **re-enable** New+ via the Settings toggle. Confirm re-enabling **copies the default example templates** into the Templates folder — `Example folder\` (containing `Example txt file.txt` and `Another example txt file.txt`) and `Any files or folders placed in the template folder are available via New+.txt` — and that these appear in the New+ submenu. (The copy is driven by `CopyTemplateExamples` on the Settings-UI enable transition, and only when the folder was empty; a runner-only restart does **not** copy templates.)
- [ ] **[ADMIN: NO]** (L979) Test **Hide template filename extension** (Settings → New+; key `HideFileExtension`). With a `MyTemplate.txt` template: **ON** → the submenu caption is **`MyTemplate`** (extension hidden); **OFF** → the caption is **`MyTemplate.txt`**. In **both** cases the **created file keeps its extension** (`MyTemplate.txt`) — the option affects the menu caption only. Takes effect without an Explorer restart (handler re-reads settings per menu build).
- [ ] **[ADMIN: NO]** (L980) Test **Hide template filename starting digits, spaces and dots** (key `HideStartingDigits`). Use a digit-prefixed template `01. Digits.txt` (set *Hide extension* OFF to isolate this option): **ON** → **both** the submenu caption **and** the created filename are **`Digits.txt`** (leading `01. ` stripped from both); **OFF** → both are **`01. Digits.txt`** (preserved). Unlike *Hide extension*, this transform applies to the created filename as well as the caption.
