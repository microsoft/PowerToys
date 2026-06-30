<#
.SYNOPSIS
    End-to-end test helper for invoking a PowerScript from Keyboard Manager (new editor).

.DESCRIPTION
    Self-contained KBM e2e that doesn't require the full PowerToys runner:

      1. Forces the *new* Keyboard Manager editor (useNewEditor = true).
      2. Launches PowerToys.KeyboardManagerEditorUI.exe so you can add a shortcut whose
         action is "PowerScript" -> pick a system script (e.g. "Volume Up") -> Save.
      3. Starts PowerToys.KeyboardManagerEngine.exe standalone, which reads the saved
         default.json and installs the keyboard hook. Press your shortcut and the engine
         runs PowerScripts.Host.exe run <id>.

    Defaults assume a Debug build under <repo>\x64\Debug. Use -Configuration Release for a
    release layout.

.EXAMPLE
    # Configure a hotkey, then start the engine and test:
    pwsh -File kbm-e2e.ps1

.EXAMPLE
    # Skip the editor; just (re)start the engine to apply the current mappings:
    pwsh -File kbm-e2e.ps1 -EngineOnly
#>
[CmdletBinding()]
param(
    [switch]$EngineOnly,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

# Repo root = four levels up from src\modules\PowerScripts.
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$binRoot = Join-Path $repoRoot "x64\$Configuration"
$editorExe = Join-Path $binRoot 'WinUI3Apps\PowerToys.KeyboardManagerEditorUI.exe'
$engineExe = Join-Path $binRoot 'KeyboardManagerEngine\PowerToys.KeyboardManagerEngine.exe'
$kbmDir = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\Keyboard Manager'
$settings = Join-Path $kbmDir 'settings.json'

function Stop-ProcessesByName([string[]]$names)
{
    $ids = Get-Process -ErrorAction SilentlyContinue | Where-Object { $names -contains $_.Name } | Select-Object -ExpandProperty Id
    foreach ($id in $ids) { try { Stop-Process -Id $id -Force } catch { } }
}

if (-not (Test-Path $engineExe)) { throw "Engine not found: $engineExe. Build KeyboardManagerEngine first." }

# 1. Force the new editor.
if (Test-Path $settings)
{
    $json = Get-Content $settings -Raw | ConvertFrom-Json
    if ($json.properties.PSObject.Properties.Name -contains 'useNewEditor')
    {
        $json.properties.useNewEditor = $true
    }
    ($json | ConvertTo-Json -Depth 10) | Set-Content $settings -Encoding UTF8
    Write-Host 'Set useNewEditor = true.'
}

# 2. Launch the new editor (unless engine-only) and wait for the user to finish.
if (-not $EngineOnly)
{
    if (-not (Test-Path $editorExe)) { throw "Editor not found: $editorExe. Build KeyboardManagerEditorUI first." }

    Write-Host ''
    Write-Host 'Opening the NEW Keyboard Manager editor.' -ForegroundColor Cyan
    Write-Host '  - Click "Add shortcut", set a trigger (e.g. Ctrl+Alt+U).'
    Write-Host '  - Action type -> PowerScript -> pick a System script (e.g. Volume Up).'
    Write-Host '  - Save, then CLOSE the editor window to continue.'
    Write-Host ''

    # Pass this process id as the parent so the editor stays open until you close it.
    $editor = Start-Process -FilePath $editorExe -ArgumentList "$PID" -PassThru
    $editor.WaitForExit()
    Write-Host 'Editor closed.'
}

# 3. (Re)start the engine standalone so it applies the saved mappings.
Stop-ProcessesByName @('PowerToys.KeyboardManagerEngine')
Start-Sleep -Milliseconds 500
$engine = Start-Process -FilePath $engineExe -PassThru
Start-Sleep -Seconds 1

if (Get-Process -Id $engine.Id -ErrorAction SilentlyContinue)
{
    Write-Host ''
    Write-Host "KBM engine running (pid $($engine.Id))." -ForegroundColor Green
    Write-Host 'Press your configured shortcut now — the PowerScript should run.'
    Write-Host "Stop the engine when done:  Stop-Process -Id $($engine.Id)"
}
else
{
    throw 'Engine exited immediately. Check the KBM logs under the Keyboard Manager\Logs folder.'
}
