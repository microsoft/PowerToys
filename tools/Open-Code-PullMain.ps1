<#
    Open-Code-PullMain.ps1
    - Updates repo at $RepoPath to latest origin/main (fast-forward or rebase; optional hard reset)
    - Opens VS Code with that folder

    Usage examples:
      pwsh -NoProfile -ExecutionPolicy Bypass -File "tools/Open-Code-PullMain.ps1"
      pwsh -NoProfile -ExecutionPolicy Bypass -File "tools/Open-Code-PullMain.ps1" -RepoPath 'C:\PowerToys' -HardReset
#>
[CmdletBinding()]
param(
  [string]$RepoPath = 'C:\PowerToys',
  [switch]$HardReset
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Info($msg) { Write-Host "[INFO] $msg" -ForegroundColor Cyan }
function Warn($msg) { Write-Warning $msg }
function Err($msg)  { Write-Host "[ERROR] $msg" -ForegroundColor Red }

function Assert-Git() {
  if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git 未安装或未在 PATH 中。请先安装 Git 并确保可执行 'git'."
  }
}

function Invoke-Git([string[]]$GitArgs) {
  Info ("git " + ($GitArgs -join ' '))
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = 'git'
  $psi.Arguments = ($GitArgs -join ' ')
  $psi.WorkingDirectory = $RepoPath
  $psi.UseShellExecute = $false
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError  = $true
  $p = [System.Diagnostics.Process]::Start($psi)
  $out = $p.StandardOutput.ReadToEnd()
  $err = $p.StandardError.ReadToEnd()
  $p.WaitForExit()
  if ($out) { Write-Host $out.Trim() }
  if ($p.ExitCode -ne 0) { throw "git 失败($($p.ExitCode)): $err" }
}

function Invoke-GitOut([string[]]$GitArgs) {
  # Same as Invoke-Git but returns stdout (trimmed) and does not echo it
  Info ("git " + ($GitArgs -join ' '))
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = 'git'
  $psi.Arguments = ($GitArgs -join ' ')
  $psi.WorkingDirectory = $RepoPath
  $psi.UseShellExecute = $false
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError  = $true
  $p = [System.Diagnostics.Process]::Start($psi)
  $out = $p.StandardOutput.ReadToEnd()
  $err = $p.StandardError.ReadToEnd()
  $p.WaitForExit()
  if ($p.ExitCode -ne 0) { throw "git 失败($($p.ExitCode)): $err" }
  return $out.Trim()
}

function Ensure-Repo() {
  if (-not (Test-Path -LiteralPath $RepoPath)) {
    throw "路径不存在: $RepoPath"
  }
  Set-Location -LiteralPath $RepoPath

  $isRepo = Test-Path -LiteralPath (Join-Path $RepoPath '.git')
  if (-not $isRepo) {
    throw "$RepoPath 不是 git 仓库（缺少 .git）。请确认路径是否正确。"
  }
}

function Update-Main() {
  Invoke-Git -GitArgs @('fetch','origin','--prune')

  # Ensure local main exists (create tracking branch if missing)
  $hasLocalMain = $true
  try {
    Invoke-Git -GitArgs @('show-ref','--verify','--quiet','refs/heads/main')
  } catch {
    $hasLocalMain = $false
  }
  if (-not $hasLocalMain) {
    Info "创建本地 main 跟踪分支 -> origin/main"
    Invoke-Git -GitArgs @('branch','--track','main','origin/main')
  }

  # Get current branch without switching
  $current = Invoke-GitOut -GitArgs @('rev-parse','--abbrev-ref','HEAD')
  Info "当前分支: $current"

  if ($current -ne 'main') {
    # Don’t checkout; just fast-forward or force-move main ref to origin/main
    Info "更新 refs/heads/main 指向 origin/main（不切换分支）"
    Invoke-Git -GitArgs @('branch','-f','main','origin/main')
  } else {
    if ($HardReset.IsPresent) {
      Warn "当前在 main，执行强制重置到 origin/main（会丢弃未推送变更）"
      Invoke-Git -GitArgs @('reset','--hard','origin/main')
    } else {
      # Try fast-forward merge without additional fetch
      Info "当前在 main，执行 fast-forward 到 origin/main"
      try {
        Invoke-Git -GitArgs @('merge','--ff-only','origin/main')
      } catch {
        Warn "fast-forward 失败，可手动处理或使用 -HardReset"
      }
    }
  }
}

function Start-VSCode([string]$PathToOpen) {
  $codeCmd = Get-Command code -ErrorAction SilentlyContinue
  if ($codeCmd) {
    Info "启动 VS Code（code -r `"$PathToOpen`"）"
    Start-Process -FilePath $codeCmd.Source -ArgumentList @('-r', $PathToOpen) | Out-Null
    return
  }

  $candidates = @(
    "$env:LOCALAPPDATA\Programs\Microsoft VS Code\Code.exe",
    "$env:ProgramFiles\Microsoft VS Code\Code.exe",
    "$env:ProgramFiles(x86)\Microsoft VS Code\Code.exe"
  )
  $codeExe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
  if ($codeExe) {
    Info "启动 VS Code（$codeExe `"$PathToOpen`"）"
    Start-Process -FilePath $codeExe -ArgumentList @('-r', $PathToOpen) | Out-Null
  } else {
    throw "未找到 VS Code，可执行文件 code / Code.exe 均不可用。请安装或添加到 PATH。"
  }
}

try {
  Info "校验 Git 与仓库..."
  Assert-Git
  Ensure-Repo

  Info "同步 origin/main..."
  Update-Main

  Info "打开 VS Code..."
  Start-VSCode -PathToOpen $RepoPath

  Info "完成。"
} catch {
  Err $_
  exit 1
}
