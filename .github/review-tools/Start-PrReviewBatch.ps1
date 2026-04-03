param(
    [Parameter(Mandatory = $true)]
    [string] $CategorizedPrsPath,

    [Parameter(Mandatory = $true)]
    [string] $ReviewRoot,

    [int] $MaxConcurrent = 6,
    [int] $IdleMinutes = 5,
    [int] $MaxRetries = 2,
    [int] $PollSeconds = 20
)

$ErrorActionPreference = "Stop"

function Get-ReviewedPrNumbers {
    param([string] $Root)

    @(Get-ChildItem $Root -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName "00-OVERVIEW.md") } |
        ForEach-Object { [int]$_.Name })
}

function Get-LatestWriteTime {
    param([string] $Folder)

    if (-not (Test-Path $Folder)) {
        return $null
    }

    $files = Get-ChildItem $Folder -File -ErrorAction SilentlyContinue
    if (-not $files) {
        return $null
    }

    ($files | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime
}

function Start-PrReviewJob {
    param(
        [int] $PrNumber,
        [string] $WorkingDir
    )

    Start-Job -ScriptBlock {
        param($wd, $n)
        Set-Location $wd
        & copilot -p "Review PR #$n using the review-pr.prompt.md workflow. Write all output files to 'Generated Files/prReview/$n/'" --yolo -s 2>&1
    } -ArgumentList $WorkingDir, $PrNumber
}

if (-not (Test-Path $CategorizedPrsPath)) {
    throw "Categorized PRs file not found: $CategorizedPrsPath"
}

if (-not (Test-Path $ReviewRoot)) {
    New-Item -Path $ReviewRoot -ItemType Directory -Force | Out-Null
}

$data = Get-Content $CategorizedPrsPath -Raw | ConvertFrom-Json
$allPrs = @($data.Prs | ForEach-Object { [int]$_.Number })
$workingDir = (Get-Location).Path

$running = @{}
$retries = @{}
$failed = New-Object System.Collections.Generic.HashSet[int]

Write-Host "Starting review batch: $($allPrs.Count) PRs" -ForegroundColor Cyan

while ($true) {
    $reviewed = Get-ReviewedPrNumbers -Root $ReviewRoot
    $remaining = @($allPrs | Where-Object { $_ -notin $reviewed -and -not $failed.Contains($_) })

    if ($remaining.Count -eq 0 -and $running.Count -eq 0) {
        Write-Host "ALL DONE!" -ForegroundColor Green
        break
    }

    foreach ($entry in @($running.GetEnumerator())) {
        $pr = $entry.Key
        $job = $entry.Value
        $folder = Join-Path $ReviewRoot $pr
        $latestWrite = Get-LatestWriteTime -Folder $folder
        $idleFor = if ($latestWrite) { (New-TimeSpan -Start $latestWrite -End (Get-Date)).TotalMinutes } else { $null }

        $isDone = $job.State -in @("Completed", "Failed", "Stopped")
        $hasOverview = Test-Path (Join-Path $folder "00-OVERVIEW.md")
        $isIdleTooLong = $idleFor -ne $null -and $idleFor -ge $IdleMinutes

        if ($isDone -and -not $hasOverview) {
            $retries[$pr] = ($retries[$pr] + 1)
            if ($retries[$pr] -le $MaxRetries) {
                Write-Host "PR #$pr finished without overview. Retrying ($($retries[$pr])/$MaxRetries)..." -ForegroundColor Yellow
                Remove-Job $job -Force -ErrorAction SilentlyContinue
                $running.Remove($pr)
            } else {
                Write-Host "PR #$pr failed after $MaxRetries retries." -ForegroundColor Red
                $null = $failed.Add($pr)
                New-Item -Path (Join-Path $folder "__error.flag") -ItemType File -Force | Out-Null
                Remove-Job $job -Force -ErrorAction SilentlyContinue
                $running.Remove($pr)
            }
        } elseif (-not $hasOverview -and $isIdleTooLong) {
            $retries[$pr] = ($retries[$pr] + 1)
            if ($retries[$pr] -le $MaxRetries) {
                Write-Host "PR #$pr idle for $([int]$idleFor)m. Restarting ($($retries[$pr])/$MaxRetries)..." -ForegroundColor Yellow
                Stop-Job $job -ErrorAction SilentlyContinue
                Remove-Job $job -Force -ErrorAction SilentlyContinue
                $running.Remove($pr)
            } else {
                Write-Host "PR #$pr idle repeatedly; giving up after $MaxRetries retries." -ForegroundColor Red
                $null = $failed.Add($pr)
                New-Item -Path (Join-Path $folder "__error.flag") -ItemType File -Force | Out-Null
                Stop-Job $job -ErrorAction SilentlyContinue
                Remove-Job $job -Force -ErrorAction SilentlyContinue
                $running.Remove($pr)
            }
        } elseif ($isDone -and $hasOverview) {
            Remove-Job $job -Force -ErrorAction SilentlyContinue
            $running.Remove($pr)
        }
    }

    $reviewed = Get-ReviewedPrNumbers -Root $ReviewRoot
    $remaining = @($allPrs | Where-Object { $_ -notin $reviewed -and -not $failed.Contains($_) })

    while ($running.Count -lt $MaxConcurrent -and $remaining.Count -gt 0) {
        $next = $remaining | Select-Object -First 1
        $remaining = $remaining | Select-Object -Skip 1

        if (-not $retries.ContainsKey($next)) {
            $retries[$next] = 0
        }

        if ($retries[$next] -gt $MaxRetries) {
            continue
        }

        $job = Start-PrReviewJob -PrNumber $next -WorkingDir $workingDir
        $running[$next] = $job
        Write-Host "Started PR #$next (running: $($running.Count))" -ForegroundColor Cyan
    }

    $reviewedCount = $reviewed.Count
    $pendingCount = $remaining.Count
    Write-Host "Progress: $reviewedCount/$($allPrs.Count) complete | Running: $($running.Count) | Pending: $pendingCount | Failed: $($failed.Count)" -ForegroundColor Gray

    if ($remaining.Count -eq 0 -and $running.Count -eq 0) {
        if ($failed.Count -gt 0) {
            Write-Host "Completed with failures: $($failed.Count)." -ForegroundColor Yellow
        }
        break
    }

    Start-Sleep -Seconds $PollSeconds
}