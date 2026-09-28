# Advanced Paste CLI: E2E test kit

Run these commands in **PowerShell** on Windows after building
`src\modules\AdvancedPaste\AdvancedPaste.CLI\AdvancedPaste.CLI.csproj`.
The executable is the installed CLI target, not the PATH-visible shim; testing
the shim itself requires an installed PowerToys build.

```powershell
$repo = (Resolve-Path (Read-Host 'PowerToys repository root')).Path; $kit = Join-Path $repo 'src\modules\AdvancedPaste\AdvancedPaste.CLI\E2E'; $cli = Join-Path $repo 'x64\Debug\WinUI3Apps\PowerToys.AdvancedPaste.Cli.exe'; $out = Join-Path $env:TEMP 'AdvancedPasteCliE2E'; New-Item -ItemType Directory -Force $out | Out-Null
```

Change `$cli` for ARM64, Release, or another worktree. The input fixtures are
checked in; output is written only under `$out`, never over a fixture. Each
line below is runnable independently after the setup line. Check
`$LASTEXITCODE` immediately after the executable call (`0` success, `1`
runtime failure, `2` invalid arguments).

| Scenario | One-liner | Expected result |
| --- | --- | --- |
| Automated non-clipboard suite | `& (Join-Path $kit 'Run-E2E.ps1') -Executable $cli` | Prints `Advanced Paste CLI fixture E2E tests passed`; exit 0. |
| Help | `& $cli transform --help` | Shows formats, input/output options; exit 0. |
| Text file -> stdout | `& $cli transform --action plain-text --input (Join-Path $kit 'plain.txt') --stdout; $LASTEXITCODE` | Two unchanged text lines, then `0`. |
| HTML -> Markdown file | `& $cli transform --format markdown --input (Join-Path $kit 'article.html') --output (Join-Path $out 'article.md'); Get-Content -Raw (Join-Path $out 'article.md')` | Contains `**PowerToys**`, no `doNotIncludeThis`; CLI exit 0. |
| CSV -> JSON stdout | `& $cli transform --action json --input (Join-Path $kit 'people.csv') --stdout; $LASTEXITCODE` | Array of rows containing `Ada` and `Grace`, then `0`. |
| XML -> JSON file | `& $cli transform --format json --input (Join-Path $kit 'note.xml') --output (Join-Path $out 'note.json'); Get-Content -Raw (Join-Path $out 'note.json')` | JSON `note` object with `title` and `owner`; CLI exit 0. |
| INI -> JSON stdout | `& $cli transform --action json --input (Join-Path $kit 'config.ini') --stdout; $LASTEXITCODE` | JSON `general` object with `name: "PowerToys"`; then `0`. |
| JSON passthrough | `& $cli transform --action json --input (Join-Path $kit 'existing.json') --stdout; $LASTEXITCODE` | Original JSON unchanged; then `0`. |
| stdin -> stdout | `'Hello from stdin' \| & $cli transform --action plain-text --stdin --stdout; $LASTEXITCODE` | Prints `Hello from stdin`, then `0` (PowerShell adds a newline). |
| stdin -> default clipboard | `'Hello from stdin' \| & $cli transform --action plain-text --stdin; $code = $LASTEXITCODE; Get-Clipboard; $code` | Clipboard contains `Hello from stdin`, then `0`. |
| JSON success envelope | `'hello' \| & $cli transform --action plain-text --stdin --stdout --json; $LASTEXITCODE` | One parseable JSON object with `status: "success"` and `output: "hello\r\n"`; then `0`. |
| List actions | `& $cli actions list --json \| ConvertFrom-Json` | Lists all built-in actions and configured custom actions. |
| OCR image -> stdout | `& $cli transform --action image-to-text --input 'path\to\image.png' --stdout; $LASTEXITCODE` | Prints OCR text, then `0`. |
| Video -> MP3 file | `& $cli transform --action transcode-to-mp3 --input 'path\to\video.mp4' --output (Join-Path $out 'audio.mp3'); $LASTEXITCODE` | Creates a nonempty MP3, then `0`. |
| Paste with AI | `Set-Clipboard 'Text to summarize'; & $cli transform --action paste-with-ai --prompt 'Summarize in one sentence' --clipboard; $LASTEXITCODE` | Clipboard contains the configured provider's result, then `0`. |
| Saved custom action | `Set-Clipboard 'Input'; & $cli transform --custom-action 1 --clipboard; $LASTEXITCODE` | Clipboard contains custom action 1's result, then `0`. |
| JSON argument error | `& $cli transform --format json --stdin --clipboard --json 2>&1; $LASTEXITCODE` | One JSON error with `code: "invalid_input_mode"` and `usage`; then `2`; clipboard untouched. |
| Unsupported action | `'hello' \| & $cli transform --action ocr --stdin 2>&1; $LASTEXITCODE` | Error explaining supported actions, then `2`. |
| Empty input | `& $cli transform --format plain-text --input (Join-Path $kit 'empty.txt') 2>&1; $LASTEXITCODE` | `empty_input` message, then `1`. |
| Missing file | `& $cli transform --format json --input (Join-Path $kit 'missing.txt') --json 2>&1; $LASTEXITCODE` | JSON error `code: "io_error"`, then `1`. |
| Inaccessible output | `& $cli transform --format json --input (Join-Path $kit 'people.csv') --output $out --json 2>&1; $LASTEXITCODE` | `$out` is a directory, so JSON `code: "io_error"` and exit `1`. |
| Oversized input | `[IO.File]::WriteAllText((Join-Path $out 'oversize.txt'), ('x' * 16777217)); & $cli transform --format json --input (Join-Path $out 'oversize.txt') --json 2>&1; $LASTEXITCODE` | JSON `code: "input_too_large"` and exit `1`; file remains in `$out` until cleanup. |
| Conflicting output modes | `'hello' \| & $cli transform --format plain-text --stdin --stdout --output-clipboard --json 2>&1; $LASTEXITCODE` | JSON `invalid_output_mode`, then `2`; clipboard untouched. |

**Clipboard tests are opt-in and overwrite the current clipboard.** Only run
them after saving anything important. `Set-Clipboard` writes plain text; to
test HTML clipboard priority for Markdown, copy rendered HTML from a browser
instead of copying the literal markup below. No paste keystrokes, UI, or Runner
are involved.

| Scenario | One-liner | Expected result |
| --- | --- | --- |
| Explicit clipboard -> stdout | `Set-Clipboard -Value 'Hello from clipboard'; & $cli transform --action plain-text --clipboard --stdout; $LASTEXITCODE` | Prints `Hello from clipboard`, then `0`. |
| Explicit file -> clipboard | `& $cli transform --format plain-text --input (Join-Path $kit 'plain.txt') --output-clipboard; $code = $LASTEXITCODE; Get-Clipboard; $code` | Clipboard holds fixture text, then `0`; no transformed text is sent to stdout. |
| Clipboard Markdown | `& $cli transform --format markdown --clipboard; $LASTEXITCODE` | After copying rendered HTML, emits Markdown (e.g. bold text becomes `**bold text**`), then `0`. |

OCR, PNG-file creation, and media transcoding accept an input file or compatible
rich clipboard content. AI tests use the same provider configuration, policy,
and Credential Vault entries as the Advanced Paste UI and may incur provider
usage.

Clean up generated outputs when finished: `Remove-Item -LiteralPath $out -Recurse -Force`.
