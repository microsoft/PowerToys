# Settings Search Evaluation

This tool evaluates Settings search quality and latency for:

- `basic` search (`FuzzSearchEngine`)
- `semantic` search (`SemanticSearchEngine`)

It reports:

- `Recall@K`
- `MRR` (mean reciprocal rank)
- Search latency (`avg`, `p50`, `p95`, `max`)
- Dataset diagnostics including duplicate `SettingEntry.Id` buckets

The evaluator is standalone and does not require building/running `PowerToys.Settings.exe`.

## Run

Build with Visual Studio `MSBuild.exe` (the project references native components):

```powershell
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
& $msbuild tools\SettingsSearchEvaluation\SettingsSearchEvaluation.csproj `
  /t:Build /p:Configuration=Debug /p:Platform=arm64 /m:1 /nologo
```

Run the built executable:

```powershell
.\arm64\Debug\SettingsSearchEvaluation.exe `
  --index-json src/settings-ui/Settings.UI/Assets/Settings/search.index.json `
  --cases-json tools/SettingsSearchEvaluation/cases/settings-search-cases.sample.json `
  --engine both `
  --top-k 5 `
  --iterations 5 `
  --warmup 1 `
  --output-json tools/SettingsSearchEvaluation/artifacts/report.json
```

## Normalized corpus workflow

Export normalized corpus lines from `search.index.json`:

```powershell
.\arm64\Debug\SettingsSearchEvaluation.exe `
  --index-json src/settings-ui/Settings.UI/Assets/Settings/search.index.json `
  --export-normalized tools/SettingsSearchEvaluation/artifacts/normalized-settings-corpus.tsv `
  --export-only
```

`normalized-settings-corpus.tsv` format:

- one entry per line
- `<id>\t<normalized text>`
- plus an auto-generated text-only companion file at `normalized-settings-corpus.text.tsv`
  containing only normalized localized text values (no ID/key column), for sharing/debug.

When loading from `search.index.json`, the evaluator now resolves UID keys to real user-facing strings
from `Resources.resw` when available (auto-detected in repo layout). You can override the `.resw` path
with environment variable `SETTINGS_SEARCH_EVAL_RESW`.

Run evaluation by indexing and querying directly from the normalized corpus:

```powershell
.\arm64\Debug\SettingsSearchEvaluation.exe `
  --normalized-corpus tools/SettingsSearchEvaluation/artifacts/normalized-settings-corpus.tsv `
  --cases-json tools/SettingsSearchEvaluation/cases/settings-search-cases.sample.json `
  --engine basic `
  --top-k 5 `
  --iterations 5 `
  --warmup 1 `
  --output-json tools/SettingsSearchEvaluation/artifacts/report.basic.normalized.json
```

### Startup args file

When launched via AUMID, command-line arguments may not be forwarded by shell activation.
If the evaluator starts with no CLI args, it will read one argument per line from:

- `%LOCALAPPDATA%\PowerToys.SettingsSearchEvaluation\launch.args.txt`
- `tools/SettingsSearchEvaluation/artifacts/launch.args.txt` (repo flow)
- override path with env var `SETTINGS_SEARCH_EVAL_ARGS_FILE`

Empty lines and `#` comments are ignored.

## Full package (recommended)

Build a dedicated evaluator MSIX (independent from PowerToys sparse package):

```powershell
pwsh .\tools\SettingsSearchEvaluation\BuildFullPackage.ps1 `
  -Platform arm64 `
  -Configuration Debug `
  -Install
```

Output:

- `tools/SettingsSearchEvaluation/artifacts/full-package/SettingsSearchEvaluation.msix`

After install, launch with package identity:

```powershell
$pkg = Get-AppxPackage Microsoft.PowerToys.SettingsSearchEvaluation
Start-Process "shell:AppsFolder\$($pkg.PackageFamilyName)!SettingsSearchEvaluation"
```

### True semantic profile with packaged app

Write startup args (absolute paths recommended):

```powershell
$argsFile = Join-Path $env:LOCALAPPDATA 'PowerToys.SettingsSearchEvaluation\launch.args.txt'
New-Item -ItemType Directory -Force (Split-Path $argsFile) | Out-Null
@(
  '--normalized-corpus'
  'C:\data\normalized-settings-corpus.tsv'
  '--cases-json'
  'C:\data\settings-search-cases.sample.json'
  '--engine'
  'both'
  '--top-k'
  '5'
  '--iterations'
  '5'
  '--warmup'
  '1'
  '--semantic-timeout-ms'
  '60000'
  '--output-json'
  'C:\data\report.both.normalized.json'
) | Set-Content -LiteralPath $argsFile -Encoding UTF8
```

Then launch by AUMID (from previous section). The generated report will include semantic capability flags.

## Visualize results

Generate an HTML dashboard from one or more report JSON files:

```powershell
pwsh .\tools\SettingsSearchEvaluation\GenerateProfileDashboard.ps1 `
  -InputReports @(
    ".\tools\SettingsSearchEvaluation\artifacts\report.basic.direct.json",
    ".\tools\SettingsSearchEvaluation\artifacts\report.semantic.aumid.json"
  ) `
  -OutputHtml ".\tools\SettingsSearchEvaluation\artifacts\search-profile-dashboard.html"
```

## Case file format

```json
[
  {
    "query": "color picker",
    "expectedIds": ["ColorPicker"],
    "notes": "Module entry"
  }
]
```

If `--cases-json` is not provided, fallback cases are auto-generated from the index headers.
