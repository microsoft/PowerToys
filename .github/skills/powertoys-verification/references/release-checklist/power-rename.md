# PowerRename — PowerToys release checklist

> Source: split from `release-checklist-annotated.md` (generated 2026-06-06). One module per file.

## Legend

Each item is annotated with an admin-requirement tag:

**Admin requirement**:
- `[ADMIN: NO]` - runnable from a standard (non-elevated) shell
- `[ADMIN: YES]` - requires elevated session (writes to HKLM, %WinDir%\System32, MSI install, GPO templates, etc.)
- `[ADMIN: COND]` - conditional - the basic case is non-admin but specific sub-cases require admin (e.g. "test with elevated target app", "Restart as admin" variants)

---

## PowerRename (17 items)

- [ ] **[ADMIN: NO]** (L393) Toggle the PowerRename enable switch **off**, then **on**, in PowerToys Settings. Confirm the `Rename with PowerRename` context-menu entry **disappears** when the module is disabled and **reappears** when it is enabled. (On Win11) With the module enabled, confirm `Rename with PowerRename` is present in **both** the Win11 tier-1 (modern) context menu **and** the classic "Show more options" (`#32768`) context menu.
- [ ] **[ADMIN: NO]** (L394) **Shell integration — verify the `Show PowerRename in` dropdown together with the `Hide icon in context menu` checkbox across all 4 combinations.** Both live under Settings → PowerRename → *Shell integration*; **expand the "Show PowerRename in" row** (the ˅ at the far right, not the dropdown arrow) to reveal the "Hide icon in context menu" checkbox. The icon setting applies to **both** the modern (Win11 tier-1) and legacy/extended menus. Verify all four:
  - **`Default and extended context menu` + `Hide icon` unchecked** → `Rename with PowerRename` appears in the **default (modern) right-click menu, with** its icon.
  - **`Default and extended context menu` + `Hide icon` checked** → appears in the default menu, **without** an icon.
  - **`Extended context menu only` + `Hide icon` unchecked** → **absent from the default menu**; appears **only under Shift + right-click, with** its icon.
  - **`Extended context menu only` + `Hide icon` checked** → absent from the default menu; appears **only under Shift + right-click, without** an icon.
- [ ] **[ADMIN: NO]** (L396) Toggle **autocomplete** (Settings → PowerRename → *Enable auto-complete for the search & replace fields*; settings key `MRUEnabled`, read at PR launch). With it **on** and prior search history present, launch PowerRename, focus the **Search** field and type a prefix (e.g. `p`) → a `SuggestionsPopup`/`SuggestionsList` of matching prior terms **appears**. With it **off** (relaunch), typing the same prefix shows **no** suggestions popup.
- [ ] **[ADMIN: NO]** (L397) Toggle **Show recently used strings** (settings key `PersistState`, read at PR launch). With it **on**: run PowerRename once with Search=`persist`, Replace=`KEPT`, and Apply; relaunch on a fresh file → the Search/Replace fields are **pre-populated** with `persist`/`KEPT` (restored from `power-rename-last-run-data.json`) and the preview auto-applies. With it **off**: every launch starts with **empty** Search/Replace fields.
- [ ] **[ADMIN: NO]** (L399) With `Hi World.txt` selected and a Replace producing `Hi World`, click each case toggle in turn and confirm the preview **Renamed** column: **Uppercase** → `HI WORLD.TXT`, **Titlecase** → `Hi World.txt`, **Lowercase** → `hi world.txt`. Confirm the buttons are **mutually exclusive** — selecting one **deselects** the previously active case button (only one active at a time).
- [ ] **[ADMIN: NO]** (L400) Selection = one file (`aaa_file.txt`) + one folder (`aaa_folder` containing `aaa_inner.txt`); Search `aaa` → Replace `zzz`. Toggle **Include Files**, **Include Folders**, and **Include Subfolder Items** independently and confirm each gates only its row type in the preview:
  - **All include on** → all three renamed (`zzz_file.txt`, `zzz_folder`, `zzz_inner.txt`).
  - **Files off** → files' Renamed column empty; only `aaa_folder → zzz_folder`.
  - **Folders off** → folder unchanged; both files renamed.
  - **Subfolder Items off** → inner file excluded; top-level file + folder renamed.

  Confirm the toggles **combine** (several can be off simultaneously).
- [ ] **[ADMIN: NO]** (L401) File `aaa.aaa`, Search `aaa` → Replace `zzz`, Match-all on. Using the **Apply to** selector (single-select), confirm each scope changes only the targeted part of the name: **Filename + extension** (default) → `aaa.aaa → zzz.zzz`; **Filename only** → `aaa.aaa → zzz.aaa`; **Extension only** → `aaa.aaa → aaa.zzz`. Only **one** option can be selected at a time.
- [ ] **[ADMIN: NO]** (L402) Enumerate Items. Test advanced enumeration using different values for every field `${start=10,increment=2,padding=4}`. With Enumerate items on and Replace = `item_${start=10,increment=2,padding=4}` applied to a 5-file selection, the preview **Renamed** column must show `item_0010`, `item_0012`, `item_0014`, `item_0016`, `item_0018` (counter = `start + index*increment` for 0-based index, zero-padded to `padding` digits; verify each field independently: `start=10` → first value 0010 not 0, `increment=2` → step of 2 not 1, `padding=4` → 4-digit zero-pad not unpadded).
- [ ] **[ADMIN: NO]** (L403) File `MixedCase.txt`, Search `mixed` → Replace `XXX`. With **Case sensitive OFF** (default) → `mixed` matches `Mixed`, preview `MixedCase.txt → XXXCase.txt`. With **Case sensitive ON** → no match: the Renamed column is **empty** and the **Apply** button is **disabled**.
- [ ] **[ADMIN: NO]** (L404) File `Foo_A_A_A.txt`, Search `A` → Replace `B`. With **Match all occurrences unchecked** (default) → only the **first** (left-to-right) match is replaced: `Foo_A_A_A.txt → Foo_B_A_A.txt`. With it **checked** → **all** occurrences are replaced: `Foo_A_A_A.txt → Foo_B_B_B.txt`.
- [ ] **[ADMIN: NO]** (L406) Files `IMG_001.png`..`IMG_003.png`. Enable **Use regular expressions** and set Search = `(.*).png`. Confirm the regex matches every filename (capture group populated) — with Replace = `foo_$1.png` the preview shows `IMG_00n.png → foo_IMG_00n.png` for n=1..3.
- [ ] **[ADMIN: NO]** (L407) Regex on, Search = `(.*).png`, Replace = `foo_$1.png` on files `IMG_001.png`..`IMG_003.png`. Confirm the capture-group back-reference `$1` is substituted with the matched stem: `IMG_001.png → foo_IMG_001.png`, `…002 → foo_IMG_002.png`, `…003 → foo_IMG_003.png` (with regex off, `$1` would appear literally).
- [ ] **[ADMIN: NO]** (L408) File `orig.txt` with a known creation time (e.g. `07/02/2026 11:54:54`). With regex on, Search = `.*\.txt$`, Replace = `$YYYY_$MMMM_$DD__$hh-$mm-$ss.txt`. Confirm each token expands from the file's creation date/time so the preview exactly matches the expected string `2026_July_02__11-54-54.txt` (`$YYYY`→`2026`, `$MMMM`→`July`, `$DD`→`02`, `$hh`→`11`, `$mm`→`54`, `$ss`→`54`; also verify `$fff` milliseconds).
- [ ] **[ADMIN: NO]** (L409) Enable **Use Boost library** (settings key `UseBoostLib`, read at PR launch — relaunch after toggling). Files `test.txt` and `nest.txt`; with regex on, Search = `(?<=t)est` (Perl lookbehind, unsupported by the default std::regex engine), Replace = `X`. Confirm the lookbehind evaluates without error and matches **only** where `est` is preceded by `t`: `test.txt → tX.txt`, while `nest.txt` is **unchanged**.
- [ ] **[ADMIN: NO]** (L411) Files `file1.txt, file2.txt, file3.txt`; Search `file` → Replace `done`. Uncheck the `file2.txt` row in the preview (row shows `[ ]`), then **Apply**. Confirm on disk that only the checked rows were renamed: `done1.txt, done3.txt, file2.txt` — the unchecked file is **left untouched**.
- [ ] **[ADMIN: NO]** (L412) Files `match1.txt, match2.txt, keepme.txt`; Search `match` → Replace `X` (`keepme.txt` does not match). Use the **Filter** (funnel) button above the file list: choosing **"Only show files that will be renamed"** hides the non-matching `keepme.txt` (only `match1.txt`, `match2.txt` visible); choosing **"Show all files"** restores all 3 rows.
- [ ] **[ADMIN: NO]** (L413) With a 3-file list all initially checked, use the **Select/deselect all** checkbox (header "Select or deselect all") above the file list: one click **deselects all** (all rows `[ ]`); clicking again **selects all** (all rows `[x]`) — toggling every row in a single action.