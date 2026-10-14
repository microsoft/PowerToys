# Advanced Paste

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/advanced-paste)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Advanced%20Paste%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Advanced%20Paste%22%20label%3AIssue-Bug)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen++label%3A%22Product-Advanced+Paste%22)

## Overview

Advanced Paste is a PowerToys module that provides enhanced clipboard pasting with formatting options and additional functionality.

## Implementation Details

[Source code](/src/modules/AdvancedPaste)

TODO: Add implementation details

### Headless CLI

`PowerToys.AdvancedPaste.CLI.exe` runs Advanced Paste actions without starting the Advanced Paste UI or communicating with Runner. It loads the same shared action engine, a startup snapshot of settings, policy, credential-vault entries, AI providers, OCR, and media-transcoding implementation used by the UI; live settings-file watching is disabled for the one-shot CLI process.

```powershell
Get-Clipboard | PowerToys.AdvancedPaste.CLI.exe transform --action plain-text --stdin --stdout
PowerToys.AdvancedPaste.CLI.exe transform --action markdown --input notes.html --output notes.md
PowerToys.AdvancedPaste.CLI.exe transform --action image-to-text --input screenshot.png --stdout
PowerToys.AdvancedPaste.CLI.exe transform --action transcode-to-mp3 --input recording.mp4 --output recording.mp3
PowerToys.AdvancedPaste.CLI.exe transform --action paste-with-ai --prompt "Summarize this" --clipboard
PowerToys.AdvancedPaste.CLI.exe transform --custom-action 3 --clipboard
# After copying a CSV file in Explorer, read it from the default clipboard input.
PowerToys.AdvancedPaste.CLI.exe transform --action json --stdout
```

Input defaults to the clipboard; use at most one of `--input <path>`, `--stdin`, or `--clipboard` to select a source explicitly. Piped input requires `--stdin`; redirecting standard input does not override the clipboard default. Output also defaults to the clipboard; use at most one of `--output <path>`, `--stdout`, or `--output-clipboard`. Clipboard output sets content but never simulates paste keys. Text inputs are limited to 16,777,216 characters and raw input, prompts, and output are never written to the CLI log. `--format` remains an alias for `--action`.

Clipboard modes use the Win32/OLE clipboard so they do not require a foreground app window. Like the Windows clipboard itself, they require an interactive user session; use file or standard-stream modes for services and session-0 automation.

Built-in actions are `plain-text`, `markdown`, `json`, `fix-spelling-and-grammar`, `image-to-text`, `paste-as-txt-file`, `paste-as-png-file`, `paste-as-html-file`, `transcode-to-mp3`, `transcode-to-mp4`, and `paste-with-ai`. Use `actions list` to include configured custom actions. The CLI honors the Advanced Paste enabled policy and AI actions honor the configured provider, AI-related GPO, moderation, and credentials; `--provider <id>` selects another configured provider.

Root `--help` and `transform --help` describe every built-in action, input/output defaults, AI/custom-action setup, examples, size limits, cancellation, and exit codes without loading settings or providers. `actions list` includes descriptions; its JSON array uses keys `name`, `kind`, `id`, `requiresPrompt`, and `description`. Saved custom actions are discovered through `actions list`, since they depend on the user's configuration.

#### AI providers

A provider is an AI service and model configured in **PowerToys Settings > Advanced Paste**, such as Ollama with a local model or OpenAI with a cloud model. Provider IDs are generated per configured entry; there is no fixed list of IDs, and names such as `Ollama` are not IDs.

```powershell
PowerToys.AdvancedPaste.CLI.exe providers list
PowerToys.AdvancedPaste.CLI.exe providers list --json
PowerToys.AdvancedPaste.CLI.exe transform --action paste-with-ai --prompt "Summarize in one sentence" --stdin --stdout --provider <id-from-list>
```

`providers list` shows each configured ID, service/model, default selection, and policy status without making AI requests or reading the clipboard. It lists configuration, not a connectivity or credential-health check; it also works when AI is turned off in settings. JSON output is an array with stable keys `id`, `name`, `serviceType`, `model`, `isDefault`, and `isAllowedByPolicy`. API keys, endpoints, and system prompts are not included.

`--provider` applies only to `paste-with-ai`, `fix-spelling-and-grammar`, and `--custom-action`. It overrides the saved action's provider or the default provider for that invocation, including nested Advanced AI transformations and cached action chains. Unknown explicit IDs are rejected rather than falling back to another provider. Without the option, the saved action's configured provider is used when present, otherwise the default provider. Plain-text, Markdown, JSON, OCR, and media conversion do not use this AI-provider option.

Configure providers and credentials in Settings; the CLI does not enable AI, create providers, sign in, install local models, or accept API keys. Local Ollama needs a running server and an installed model with a matching model name; cloud services need their own API access and can incur charges. A GitHub Copilot subscription is not an Advanced Paste provider.

Text files are exposed as text/HTML, not as image/file attachments; malformed UTF-8 and binary files retain their original file representation. With default clipboard input or explicit `--clipboard`, a single file copied in Explorer is read using the same bounded file reader as `--input`, enabling copied CSV-to-JSON and HTML-to-Markdown conversions. Existing clipboard text, HTML, or bitmap takes precedence; folders and multiple-file selections are not converted to text or combined. Markdown and custom AI actions also accept HTML-only clipboard content. OpenAI moderation is skipped, with a warning in the log, when OpenAI or online AI is forbidden by policy; an otherwise permitted Azure provider can still run.

Generated file output is cleaned after copying to an explicit output path. Older owned temporary directories are cleaned only when clipboard input provides a snapshot (default or explicit `--clipboard`), excluding directories containing current clipboard files or the selected output. Cleanup uses paths from the original file-drop snapshot even after text-file input has been converted to text/HTML. File/stdin commands and action listing never read the clipboard for cleanup.

For `transform`, `--json` emits one UTF-8 JSON result envelope on stdout (`status`, `action`, `resultKind`, `outputPath`, `outputClipboard`, and optional `output`) or one error envelope on stderr (`status`, `code`, `message`, and `usage` for argument errors). `actions list --json` emits a bare array of action objects (`name`, `kind`, optional `id`, and `requiresPrompt`). The stable exit codes are `0` for success, `1` for input, I/O, cancellation, provider, clipboard, or transformation failures, and `2` for parser/argument errors.

Run `src\modules\AdvancedPaste\AdvancedPaste.CLI\SmokeTest.ps1` after building the CLI to exercise help, stdin/stdout, file input/output, JSON output, and failing argument paths against the built executable.

The CLI uses the centrally pinned System.CommandLine 2.0 API. Register options and subcommands through `Options` and `Subcommands`, read parsed values with `ParseResult.GetValue`, and route help through `InvocationConfiguration.Output` and `Error`. Keep option-value token checks when detecting help so a prompt or file path such as `-h` is not treated as a help request. `AdvancedPaste.Cli.UnitTests` covers these cases, command aliases, JSON output, and exit codes.

### Paste with AI Preview

The "Show preview" setting (`ShowCustomPreview`) controls whether AI-generated results are displayed in a preview window before pasting. **The preview feature does not consume additional AI credits**—the preview displays the same AI response that was already generated, cached locally from a single API call.

The implementation flow:
1. User initiates "Paste with AI" action
2. A single AI API call is made via `ExecutePasteFormatAsync`
3. The result is cached in `GeneratedResponses`
4. If preview is enabled, the cached result is displayed in the preview UI
5. User can paste the cached result without any additional API calls

See the `ExecutePasteFormatAsync(PasteFormat, PasteActionSource)` method in `OptionsViewModel.cs` for the implementation.

## Debugging

Advanced Paste is an unpackaged, self-contained WinUI 3 app (`PowerToys.AdvancedPaste.exe`). To call Windows AI APIs (Phi Silica / `Microsoft.Windows.AI.Text.LanguageModel`) it acquires **package identity** at runtime via a shared sparse MSIX package (`Microsoft.PowerToys.SparseApp`).

### Running and attaching the debugger

1. Set the **Runner** project (`src/runner`) as the startup project in Visual Studio.
2. Launch the Runner (F5). This starts the PowerToys tray icon and loads all module interfaces.
3. Open Settings (right-click tray icon → Settings) and enable the **Advanced Paste** module if it isn't already. The module launches `PowerToys.AdvancedPaste.exe` in the background immediately.
4. In Visual Studio, go to **Debug → Attach to Process** (`Ctrl+Alt+P`) and attach to `PowerToys.AdvancedPaste.exe` (select **Managed (.NET Core)** debugger).

Alternatively, use the VS Code launch configuration **"Run AdvancedPaste"** from [.vscode/launch.json](/.vscode/launch.json) to launch the exe directly — but note that without the Runner, IPC and hotkeys won't work.

### Sparse package identity (local development)

#### Why is this needed?

- The `LanguageModel` API requires a Limited Access Feature (LAF) unlock, which only succeeds when the calling process has a matching package identity.
- Advanced Paste is an unpackaged, self-contained WinUI 3 app. The sparse package grants it identity without converting it to a full MSIX.
- The csproj uses `<ProjectPriFileName>PowerToys.AdvancedPaste.pri</ProjectPriFileName>` (matching the convention of other WinUI3 apps like ImageResizer). This requires WindowsAppSDK Foundation >= 2.0.22 ([PR #6376](https://github.com/microsoft/WindowsAppSDK/pull/6376)) which fixes MRT PRI lookup under sparse identity so `Application.LoadComponent` resolves custom-named PRI files instead of hard-coding `resources.pri`.

#### One-step dev setup

```powershell
pwsh src/PackageIdentity/BuildSparsePackage.ps1 -Platform ARM64 -Configuration Debug -DevRegister
```

`-DevRegister`:
1. Generates a dev certificate under `src/PackageIdentity/.user/` (first run only).
2. Auto-imports that certificate into `CurrentUser\TrustedPeople` and `CurrentUser\Root` so the OS grants sparse identity to AP (without trust, `GetPackageFamilyName` returns `APPMODEL_ERROR_NO_PACKAGE` and LAF unlock silently fails).
3. Removes any prior registration.
4. Rewrites the publisher in a temp copy of `AppxManifest.xml` to match the dev cert subject.
5. Registers via `Add-AppxPackage -Register … -ExternalLocation X:\…\<Platform>\<Config>\WinUI3Apps`.

After registration verify:

```powershell
$pkg = Get-AppxPackage -Name '*SparseApp*'
$pkg.PackageFamilyName    # Microsoft.PowerToys.SparseApp_<PublisherId>
$pkg.PublisherId          # djwsxzxb4ksa8
$pkg.IsDevelopmentMode    # True
```

Confirm AP picks up sparse identity at runtime:

```powershell
& 'ARM64\Debug\WinUI3Apps\PowerToys.AdvancedPaste.exe' --check-phi-silica
# Exit 0 = Available, 1 = NotReady, 2 = NotSupported
```

Re-register after rebuilding AP, changing `src/PackageIdentity/AppxManifest.xml`, or switching platforms/configurations by re-running the same command. Unregister with `-Unregister`.

#### Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| `GetPackageFamilyName` returns `APPMODEL_ERROR_NO_PACKAGE` (15700) at runtime; LAF unlock returns `Unavailable` | Dev certificate not trusted (or sparse package not registered) | Re-run `BuildSparsePackage.ps1 -DevRegister` — auto-imports the cert into `TrustedPeople` and `Root`. |
| `Microsoft.UI.Xaml.dll` crash with `0xC000027B` (class-not-registered) on AP or Settings startup | `<Application>` `Executable` path in `src/PackageIdentity/AppxManifest.xml` does not resolve under the registered `ExternalLocation` (`<Config>\WinUI3Apps\`) | Confirm every `Executable` is relative to `WinUI3Apps\` (per #47177) and the file exists under the build output. |
| AP launches but never shows a window when triggered via hotkey | Runner's pipe-server wait timed out before AP's cold-start finished bootstrapping WinAppSDK + DI host | Already mitigated by the 15 s pipe timeout in `AdvancedPasteProcessManager.cpp`; warm-start launches connect in well under 1 s. |
| `XamlParseException` / `ms-appx:///Microsoft.UI.Xaml/Themes/…` not found | WindowsAppSDK Foundation < 2.0.22; MRT can't resolve custom PRI name under sparse identity | Ensure `Microsoft.WindowsAppSDK.Foundation` >= 2.0.22 in `Directory.Packages.props`. |

### How Settings UI checks Phi Silica availability

Settings UI does not have sparse package identity. To check whether Phi Silica is available, it launches Advanced Paste as a short-lived subprocess:

```
PowerToys.AdvancedPaste.exe --check-phi-silica
```

`Program.Main` recognizes this flag, calls `PhiSilicaLafHelper.TryUnlock()` + `LanguageModel.GetReadyState()`, prints one of `Available` / `NotReady` / `NotSupported` to stdout, and exits with the matching code (0/1/2). Settings reads stdout with a 10 s wait. Because each call is a fresh process, transient `Unavailable` results are not cached across checks.

### See also

- [Phi Silica local testing & troubleshooting guide](advancedpaste-phisilica-local-testing.md) — layer-by-layer diagnostics for Phi Silica availability
- [`src/PackageIdentity/readme.md`](/src/PackageIdentity/readme.md) — full sparse package documentation
- [microsoft/microsoft-ui-xaml#10856](https://github.com/microsoft/microsoft-ui-xaml/issues/10856) — original WinUI sparse-identity PRI bug
- [microsoft/WindowsAppSDK#6376](https://github.com/microsoft/WindowsAppSDK/pull/6376) — MRT sparse PRI fix (Foundation >= 2.0.22)

## Settings

| Setting | Description |
|---------|-------------|
| `ShowCustomPreview` | When enabled, shows AI-generated results in a preview window before pasting. Does not affect AI credit consumption. |

## Future Improvements

TODO: Add potential future improvements
