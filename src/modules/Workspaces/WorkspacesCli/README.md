# Workspaces CLI

A lightweight command-line wrapper around the existing Workspaces executables. Build outputs land alongside the other Workspaces tools (for example, `C:\PowerToys\ARM64\Debug\PowerToys.WorkspacesCli.exe`).

## Commands

- `snapshot [--id <workspaceId>] [--force]` – forwards to `PowerToys.WorkspacesSnapshotTool.exe`. `--force` writes directly to `workspaces.json`.
- `workspace list [--quiet]` – enumerates saved workspaces from `%LOCALAPPDATA%\Microsoft\PowerToys\Workspaces\workspaces.json`.
- `workspace launch <identifier> [--invoke-point <EditorButton|Shortcut|LaunchAndEdit>]` – resolves a workspace by id or name and invokes `PowerToys.WorkspacesLauncher.exe`.
- `open-editor` – starts `PowerToys.WorkspacesEditor.exe` without waiting for it to exit.

Example build:

```
msbuild src\modules\Workspaces\WorkspacesCli\WorkspacesCli.csproj /p:Configuration=Debug /p:Platform=ARM64 /p:SolutionDir=C:\PowerToys\ /m
```

