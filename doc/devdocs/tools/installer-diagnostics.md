# PowerToys Installer & Update Diagnostics

A step-by-step guide for diagnosing installer and update issues reported by users.

## Quick Reference: Key Files

| File/Folder | Path | Contains |
|---|---|---|
| UpdateState.json | `%LOCALAPPDATA%\Microsoft\PowerToys\UpdateState.json` | Persisted update state machine |
| Runner logs | `%LOCALAPPDATA%\Microsoft\PowerToys\RunnerLogs\runner-log_*.log` | Startup, update checks, cleanup |
| Update logs | `%LOCALAPPDATA%\Microsoft\PowerToys\UpdateLogs\update-log_*.log` | PowerToys.Update.exe activity |
| Updates folder | `%LOCALAPPDATA%\Microsoft\PowerToys\Updates\` | Downloaded installer files |

> **Note:** These paths use `%LOCALAPPDATA%` (per-user AppData) regardless of whether PowerToys was installed per-user or per-machine. The data/settings location is always per-user.

## Update State Values

From `src/common/updating/updateState.h` (`UpdateState::State` enum):

| Value | Name | Meaning |
|---|---|---|
| 0 | upToDate | No update needed |
| 1 | errorDownloading | Download or install failed, will retry |
| 2 | readyToDownload | New version found, not yet downloaded |
| 3 | readyToInstall | Installer downloaded, waiting for user action |
| 4 | networkError | GitHub API call failed |

---

## Symptom: Old update installers accumulating on disk

### What to ask the user for

1. Contents of `UpdateState.json`
2. Runner logs (last few days from `RunnerLogs\`)
3. Update logs (from `UpdateLogs\`, if they exist)
4. List of files in `Updates\` folder (names + sizes)

### Step 1: Check the running version

In runner logs, look for the startup line:

```
[info] Scoobe: product_version=v0.XX.X last_version_run=v0.XX.X
```

- **If version < v0.73.0**: The pre-download cleanup (PR #27908) is missing. Each downloaded installer accumulates because cleanup only runs at startup when state is `upToDate`. Ask the user to manually upgrade to the latest version.
- **If version >= v0.73.0**: The pre-download cleanup exists. Accumulation should not happen under normal conditions. Continue to Step 2.

### Step 2: Check UpdateState.json

```jsonc
{"state": 3, "downloadedInstallerFilename": "powertoyssetup-0.98.1-x64.exe" /* additional fields may be present */}
```

- **state = 0 (upToDate)**: Cleanup should run at startup. If files are accumulating, check runner logs for "Failed to delete" warnings (Step 4).
- **state = 3 (readyToInstall)**: An installer is downloaded but never installed. Cleanup at startup is skipped (by design, to preserve the pending installer). On v0.73+, cleanup can still occur when a future update check triggers a new download (pre-download cleanup path).
- **state = 1 (errorDownloading)**: A previous download or install failed. Startup cleanup is skipped (state is not `upToDate`). On v0.73+, cleanup runs before the next installer download is attempted.
- **state = 2 or 4**: Startup cleanup is skipped. On v0.73+, cleanup runs before the next installer download is attempted.

### Step 3: Check if PowerToys.Update.exe has ever run

- **UpdateLogs directory missing**: This suggests `PowerToys.Update.exe` may never have been launched, or it did not progress far enough to create logs. The user may never have triggered an install, or Stage 1 may have failed before Stage 2 could run.
- **UpdateLogs exist but show only "logger is initialized"**: The exe launched but the command-line argument didn't match any action (possible argument parsing issue).
- **UpdateLogs show install activity**: The update process ran. Check for success/failure.

### Step 4: Check runner logs for cleanup evidence

Search for these patterns:

| Log pattern | Meaning |
|---|---|
| `Failed to delete installer file ... Access is denied` | File locked by AV, another process, or permissions issue |
| `Failed to delete log file ...` | Same, for old log files |
| `Discovered new version` | Periodic update check ran |
| `New version is already downloaded` | State is `readyToInstall` and filename matches — no re-download, no cleanup |
| No cleanup-related entries at all | Inconclusive by itself — `cleanup_updates()` is silent on success. Corroborate with the Updates folder contents (Step 5) and the running version (Step 1). |

### Step 5: Check the Updates folder contents

- **All different versions**: Cleanup likely did not run across multiple update cycles. Confirm with the running version (Step 1) and update state before concluding a state gate issue.
- **Duplicate filenames**: Unusual — would suggest repeated download without cleanup.
- **Single file matching `downloadedInstallerFilename`**: Normal for `readyToInstall` state.

### Common root causes

| Root cause | Evidence | Fix |
|---|---|---|
| Running pre-v0.73.0 binary | `product_version` < v0.73.0 in runner log | Manually upgrade to latest |
| State stuck at `readyToInstall` (pre-v0.73) | `"state": 3` in UpdateState.json, no UpdateLogs | Manually upgrade to latest |
| File lock preventing deletion | "Failed to delete ... Access is denied" in runner logs | Check AV software, reboot and retry |
| Update installer never launched | No UpdateLogs directory | Check if update notifications are disabled by GPO or setting |
| Install fails silently | UpdateLogs show init but no install activity | Check related issues: #46966, #46967, #46969 |
