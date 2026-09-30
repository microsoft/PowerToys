# Copy as UNC path — test helpers

Local, developer-only helpers to test the **Copy as UNC path** PowerScript
(`samples/copy-as-unc/`) end-to-end. These are **not** product code and are not
meant to be committed with the feature.

The script resolves a selected item on a *mapped network drive* to its UNC path
(`\\server\share\...`) via `WNetGetUniversalNameW` and copies it to the clipboard —
the same idea as the native "Copy as UNC" PowerToy (PR #46056), implemented as a
~5-line PowerScript instead of a full C++/MSIX module.

## Files

| File | Purpose |
|------|---------|
| `setup-network-drive.ps1` | Creates a loopback SMB share + maps a free drive letter, with a sample file/folder. **Run elevated.** |
| `teardown-network-drive.ps1` | Removes the share and mapped drive. **Run elevated.** |
| `test-copy-as-unc.ps1` | Automated end-to-end check: runs the script via `PowerScripts.Host.exe` and asserts the clipboard got the right UNC. |

## Manual end-to-end (Explorer right-click)

1. From an **elevated** PowerShell:
   ```powershell
   .\setup-network-drive.ps1
   ```
   Note the mapped drive letter it prints (e.g. `Z:`).
2. Make sure PowerToys (this Debug build) is running and **PowerScripts is enabled**
   in Settings.
3. In Explorer, open the mapped drive, right-click `sample.txt` (or `subfolder`) and
   choose **PowerScripts ▸ Copy as UNC path**.
   - First run shows a trust-consent prompt (new script) — approve it.
   - A small on-top box confirms what was copied.
4. Paste (Ctrl+V) anywhere — you should get `\\localhost\PSUncTest\sample.txt`.
5. Clean up (elevated):
   ```powershell
   .\teardown-network-drive.ps1
   ```

## Automated end-to-end

```powershell
# Elevated: auto-provisions a temp share, runs, asserts, tears down.
.\test-copy-as-unc.ps1

# Or point at an existing mapped network drive (no admin needed):
.\test-copy-as-unc.ps1 -DriveLetter Z
```
Prints `PASS` / `FAIL` and exits non-zero on failure.

## Notes

- Non-network items: the PowerScripts prototype can't yet hide a context-menu entry
  based on drive type, so the script still appears on local files. In that case it
  copies the original local path and says it wasn't on a network drive — so the
  action is always safe to click.
