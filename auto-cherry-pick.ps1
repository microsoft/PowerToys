# Auto-resolve cherry-pick conflicts
param([int]$MaxAttempts = 100)

$attempts = 0
while ($attempts -lt $MaxAttempts) {
    $attempts++
    
    # Check if cherry-pick is in progress
    $status = git status --porcelain
    if (-not $status) {
        Write-Host "Cherry-pick complete!" -ForegroundColor Green
        break
    }
    
    # Get conflicted files
    $conflicts = git diff --name-only --diff-filter=U
    
    if ($conflicts) {
        Write-Host "Attempt $attempts`: Resolving conflicts..." -ForegroundColor Yellow
        
        foreach ($file in $conflicts) {
            Write-Host "  Resolving: $file"
            git checkout --ours $file 2>$null
        }
        
        # Handle deleted files
        git status --short | Where-Object { $_ -match '^DU' } | ForEach-Object {
            $file = ($_ -split '\s+', 2)[1]
            Write-Host "  Removing deleted: $file"
            git rm $file 2>$null
        }
        
        git add . 2>$null
    }
    
    # Try to continue
    $result = git cherry-pick --continue 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Continued successfully" -ForegroundColor Green
    }
    elseif ($result -match 'empty') {
        Write-Host "  Skipping empty commit" -ForegroundColor Cyan
        git cherry-pick --skip 2>&1 | Out-Null
    }
    else {
        Write-Host "  Error: $result" -ForegroundColor Red
        Start-Sleep -Seconds 1
    }
}

if ($attempts -ge $MaxAttempts) {
    Write-Host "Max attempts reached. Check status manually." -ForegroundColor Red
}
