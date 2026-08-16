# Settings backup/restore production sink matrix

All production backup/restore filesystem and archive sinks are routed through
`SettingsBackupRestoreEngine` and validated rooted-handle primitives.

| Phase | Production entry point | Validated sink behavior | Status |
|---|---|---|---|
| Policy | `SettingsBackupAndRestoreUtils.GetBackupRestoreSettingsJson` | Reads the embedded trusted policy and parses it with `BackupRestorePolicy` | Integrated |
| Root selection | `SettingsBackupRestoreEngine.Backup`, `Restore`, `CreateRestorePreview`, `GetLatestManifest` | Opens settings, backup, and staging roots once; rejects root reparses and handle paths outside their roots | Integrated |
| Backup enumeration | `SettingsBackupRestoreEngine.ReadSettings` | Traverses validated directory handles and opens only policy-matched JSON files | Integrated |
| Dry-run | `SettingsBackupRestoreEngine.Backup(..., dryRun: true)` | Uses the same validated roots, policy filtering, archive extraction, and same-handle reads as backup; performs no writes | Integrated |
| Prior archive selection | `SettingsBackupRestoreEngine.GetLatestArchiveFileName` | Examines only top-level `settings_<filetime>.ptb` candidates and opens the selected archive relative to the held backup root | Integrated |
| Archive validation/extraction | `BackupRestoreArchive.ExtractToExclusiveStaging` | Validates all ZIP names and limits before writes; extracts through an exclusive random staging-root handle | Integrated |
| Export filtering | `BackupRestorePolicy.CreateExportVersion` | Applies `IgnoredSettings` and `IgnoredPTRunSettings` to JSON read from validated file handles | Integrated |
| Archive creation | `SettingsBackupRestoreEngine.CreateArchive` | Creates the final `.ptb` with create-new semantics relative to the backup-root handle and streams ZIP entries to that handle | Integrated |
| Manifest write/read | `CreateArchive`, `GetLatestManifest` | Writes and reads `manifest.json` through validated archive/staging handles | Integrated |
| Retention cleanup | `SettingsBackupRestoreEngine.RemoveOldArchives` | Deletes only validated top-level archive candidates relative to the held backup root | Integrated |
| Staging cleanup | `SecureDirectoryRoot.DeleteTree` | Deletes app-owned staging files and directories deepest-first through validated handles | Integrated |
| Restore preview | `SettingsBackupRestoreEngine.CreateRestorePreview` | Lists modules, relative paths, exclusions, create/merge/overwrite actions, and restart behavior; binds confirmation to archive name and SHA-256 | Integrated |
| Restore archive binding | `SettingsBackupRestoreEngine.Restore` | Reopens the confirmed archive, verifies its SHA-256, and extracts from that same opened handle | Integrated |
| Restore overwrite | `SecureDirectoryRoot.OpenFileForOverwrite`, `SecureFile.OverwriteAllText` | Rejects reparses and targets without exactly one link before truncating and writing the same handle | Integrated |
| Restore merge | `SettingsBackupRestoreEngine.Restore`, `JsonSettingsMerge` | Reads, merges, rechecks metadata, truncates, and writes through one held target handle | Integrated |
| Restore create | `SecureDirectoryRoot.CreateNewFile` | Creates validated non-reparse parents and the new file with create-new semantics | Integrated |
| Restore UX | `GeneralViewModel.RestoreConfigsClick`, `GeneralPage.ShowRestorePreviewAsync` | Preview failure displays an error and returns without restore; cancellation returns without restore; confirmation remains UX rather than the security boundary | Integrated |
| Restart/status | `GeneralViewModel.RestoreConfigsClick` | Writes the existing restore timestamp and requests restart only after a successful restore result | Preserved |

## Compatibility coverage

- Legacy `.ptb` ZIP shape and manifest fields are preserved.
- `IncludeFiles`, `IgnoreFiles`, `IgnoredSettings`, `IgnoredPTRunSettings`,
  `CustomRestoreSettings.overwrite`, merge behavior, and `RestartAfterRestore`
  are covered against the production policy.
- Archive validation rejects traversal, rooted/UNC entry names, ADS names,
  reserved or ambiguous Windows names, collisions, and resource-limit abuse.
- Mocked capability tests cover UNC final-path canonicalization, cloud-placeholder
  reparse metadata, absent single-link metadata, and fail-closed preview/restore.

## Physical-filesystem capability gates

No automated test contacts a real UNC share, cloud-synced folder, or non-NTFS
volume. Those environments remain lab gates for filesystem-provider behavior;
unsupported or incomplete metadata fails closed before restore writes.
