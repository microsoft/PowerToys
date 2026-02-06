# Settings Search Evaluation

This tool evaluates Settings search quality and latency for:

- `basic` search (`FuzzSearchEngine`)
- `semantic` search (`SemanticSearchEngine`)

It reports:

- `Recall@K`
- `MRR` (mean reciprocal rank)
- Search latency (`avg`, `p50`, `p95`, `max`)
- Dataset diagnostics including duplicate `SettingEntry.Id` buckets

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
.\tools\SettingsSearchEvaluation\bin\arm64\Debug\net9.0-windows10.0.26100.0\SettingsSearchEvaluation.exe `
  --index-json src/settings-ui/Settings.UI/Assets/Settings/search.index.json `
  --cases-json tools/SettingsSearchEvaluation/cases/settings-search-cases.sample.json `
  --engine both `
  --top-k 5 `
  --iterations 5 `
  --warmup 1 `
  --output-json tools/SettingsSearchEvaluation/artifacts/report.json
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
