# Settings backup/restore sink matrix

`[x]` means the source/sink and its validated replacement primitive were traced.

| Checked | Phase | Production function and exact location | Previous primitive / trust transition | Validated production primitive | Integration state |
|---|---|---|---|---|---|
| [x] | All | `GetRegSettingsBackupAndRestoreRegItem` | Registry read selects backup root | Open selected root once with `SecureDirectoryRoot.Open`; canonicalize by handle | Integrated |
| [x] | All | `GetBackupRestoreSettingsJson` | Relative process-CWD config read | Embedded trusted configuration parsed by `BackupRestorePolicy` | Integrated |
| [x] | Backup/dry-run | `BackupSettingsInternal` | `Directory.Exists`, prefix string check, optional `Directory.CreateDirectory` on caller paths | Open or create roots through validated handles and enforce final-path containment | Integrated |
| [x] | Backup/dry-run | `GetSettingsFiles`, `SettingsBackupAndRestoreUtils.cs:560-568` | Recursive path enumeration can cross reparses | Enumerate one directory at a time and validate every traversed directory and file handle | Integrated |
| [x] | Backup/dry-run | latest archive selection | Path enumeration of `settings_*.ptb` | Validate top-level entries and open the selected archive relative to the held root | Integrated |
| [x] | Backup/dry-run/restore | `GetLatestSettingsFolder`, `SettingsBackupAndRestoreUtils.cs:414-480` | Predictable `%TEMP%\PowerToys_settings_<time>` plus `ZipFile.ExtractToDirectory` | Validate every ZIP name before writes, then create random `FILE_CREATE` staging and extract through relative handles (`BackupRestoreArchive.cs:25-73,81-111`) | Executable |
| [x] | Backup/dry-run/restore | `GetSettingsFiles` on extracted folder, call sites `SettingsBackupAndRestoreUtils.cs:303,672` | Reopens extracted paths after validation | Keep staging root handle alive and open every entry relative to it (`SecureDirectoryRoot.cs:214-247`) | Primitive proven; caller integration remains |
| [x] | Backup/dry-run | `GetExportVersion`, `SettingsBackupAndRestoreUtils.cs:832-879` | `File.ReadAllText(settingsFileName)` reopens source path | `SecureFile.ReadAllText` uses the already validated handle (`SecureFile.cs:38-44`) | Executable |
| [x] | Backup/dry-run | `BackupSettingsInternal`, `SettingsBackupAndRestoreUtils.cs:690,696` | Reads current and prior JSON for comparison | Same-handle reads under their respective roots; apply `IgnoredSettings` and `IgnoredPTRunSettings` in memory (`BackupRestorePolicy.CreateExportVersion`) | Executable/tested |
| [x] | Backup | `BackupSettingsInternal`, `SettingsBackupAndRestoreUtils.cs:670,724-730` | Predictable temp directory and path-based updated-file write | `CreateExclusiveStagingDirectory` uses cryptographic names and `FILE_CREATE`; `CreateNewFile` writes the returned handle (`SecureDirectoryRoot.cs:101-146`) | Executable |
| [x] | Backup | `BackupSettingsInternal`, `SettingsBackupAndRestoreUtils.cs:753-760` | Path-based unchanged-file write | Same exclusive staging/new-file handle primitive | Executable |
| [x] | Backup | `BackupSettingsInternal`, `SettingsBackupAndRestoreUtils.cs:767-778` | Path-based manifest write | Create manifest with `CreateNewFile`; write same handle | Executable primitive |
| [x] | Backup | `BackupSettingsInternal`, `SettingsBackupAndRestoreUtils.cs:783-785` | `ZipFile.CreateFromDirectory` creates/reopens final `.ptb` by path | Create final archive relative to backup-root handle with `FILE_CREATE` and stream ZIP to that handle | Integrated |
| [x] | Backup | staging cleanup | Recursive path cleanup | Delete files and directories deepest-first through validated handles | Integrated |
| [x] | Backup/restore maintenance | `RemoveOldBackups`, `SettingsBackupAndRestoreUtils.cs:937-1001` | Path enumeration plus recursive directory/file deletion | Validate top-level archive handles and delete relative to the held backup root; staging cleanup is handle-relative and deepest-first | Integrated |
| [x] | Restore | `RestoreSettings`, `SettingsBackupAndRestoreUtils.cs:279-303` | Path existence probes and path dictionaries define roots | Open app, archive, and staging roots by handle; all child access is relative | Executable primitive |
| [x] | Restore | `RestoreSettings`, `SettingsBackupAndRestoreUtils.cs:316,321` | Backup/current JSON path reads | Same-handle `SecureFile.ReadAllText` | Executable |
| [x] | Restore overwrite | `RestoreSettings`, `SettingsBackupAndRestoreUtils.cs:328-339` | `File.WriteAllText` truncates an existing path after path-only decisions | `OpenFileForOverwrite` checks reparse metadata and `NumberOfLinks == 1` before `SecureFile.OverwriteAllText` truncates the same handle (`SecureDirectoryRoot.cs:89-96`; `SecureFile.cs:47-65`) | Executable |
| [x] | Restore merge | `RestoreSettings`, `SettingsBackupAndRestoreUtils.cs:341-344` | Reopens once to read and again to truncate/write | Read, merge, recheck metadata, truncate, and write one held handle; compatible merge is executable in `JsonSettingsMerge.cs` | Executable |
| [x] | Restore new file | `RestoreSettings`, `SettingsBackupAndRestoreUtils.cs:350-357` | Path-based parent creation and create-or-truncate write | Walk/create non-reparse parents relative to root and use `FILE_CREATE` for the file (`SecureDirectoryRoot.cs:250-294`) | Executable |
| [x] | Restore UX/status | `GetLatestSettingsBackupManifest`, `SettingsBackupAndRestoreUtils.cs:511-520` | Path-based manifest read | Open `manifest.json` relative to held staging root and read same handle | Executable primitive |
| [x] | Restore UX/status | `GeneralViewModel.RestoreConfigsClick`, `GeneralViewModel.cs:1085-1111` | Confirmation is absent; successful restore writes last-restore registry value and restarts | `RestorePreviewViewModel` lists modules, paths, exclusions, create/merge/overwrite, and restart; its security-boundary statement forbids treating confirmation as enforcement | Executable/tested model |
| [x] | Backup UX/status | `GeneralViewModel.BackupConfigsClick`, `GeneralViewModel.cs:1119-1137`; `GeneralPage.RefreshBackupRestoreStatus`, `GeneralPage.xaml.cs:136-152` | Real backup followed by dry-run; dry-run still reads archive/current roots | Reuse the same root/entry validators for both real and dry-run paths; dry-run performs no writes | Design traced |
| [x] | Registry status | `SetRegSettingsBackupAndRestoreItem`, `SettingsBackupAndRestoreUtils.cs:228-242`; callers `GeneralViewModel.cs:766,1109` | Registry writes backup directory and restore timestamp | Not a filesystem boundary; retain behavior after secure filesystem operation succeeds | Preserve |

## Archive and behavior compatibility checked

- Legacy ZIP root shape is unchanged: `manifest.json` and module-relative JSON paths.
- ZIP validation rejects traversal, rooted/UNC names, ADS names, reserved/ambiguous Windows names, and normalized case/separator collisions before staging.
- Production `IncludeFiles`, `IgnoreFiles`, `IgnoredSettings`, `IgnoredPTRunSettings`, `CustomRestoreSettings.overwrite`, and `RestartAfterRestore` are parsed and tested against the existing `backup_restore_settings.json`.
- `JsonSettingsMerge` preserves recursive object merge, scalar replacement, and the legacy behavior that filters values already present in the current array while retaining duplicates from the backup array.

## Production integration

`Settings.UI.Library` delegates backup, dry-run comparison, manifest reads, archive cleanup, preview, and restore writes to `SettingsBackupRestoreEngine`. The Settings General page displays a confirmation preview, while archive validation and handle-relative I/O remain mandatory on the subsequent restore.

UNC and OneDrive/cloud-placeholder behavior is a lab-only capability gate. No test in this change contacts a network share or a real synced folder.
