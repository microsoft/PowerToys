# PowerShell script to list all files in the SilentFilesInUseBA directory
# Usage: .\ListFiles.ps1

$directory = "c:\PowerToys\installer\PowerToysSetupVNext\SilentFilesInUseBA"

Write-Host "=== Files in SilentFilesInUseBA Directory ===" -ForegroundColor Green
Write-Host "Directory: $directory" -ForegroundColor Yellow
Write-Host ""

if (Test-Path $directory) {
    # Get all files (not directories) recursively
    $files = Get-ChildItem -Path $directory -File -Recurse | Sort-Object FullName
    
    if ($files.Count -eq 0) {
        Write-Host "No files found in the directory." -ForegroundColor Red
    } else {
        Write-Host "Found $($files.Count) file(s):" -ForegroundColor Cyan
        Write-Host ""
        
        foreach ($file in $files) {
            $relativePath = $file.FullName.Replace($directory, "").TrimStart('\')
            $size = if ($file.Length -lt 1KB) { "$($file.Length) bytes" }
                   elseif ($file.Length -lt 1MB) { "{0:N1} KB" -f ($file.Length / 1KB) }
                   else { "{0:N1} MB" -f ($file.Length / 1MB) }
            
            Write-Host "  $relativePath" -ForegroundColor White
            Write-Host "    Size: $size" -ForegroundColor Gray
            Write-Host "    Modified: $($file.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
            Write-Host ""
        }
    }
    
    # Also list directories
    $directories = Get-ChildItem -Path $directory -Directory -Recurse | Sort-Object FullName
    if ($directories.Count -gt 0) {
        Write-Host "Directories found:" -ForegroundColor Cyan
        foreach ($dir in $directories) {
            $relativePath = $dir.FullName.Replace($directory, "").TrimStart('\')
            Write-Host "  $relativePath\" -ForegroundColor Magenta
        }
    }
} else {
    Write-Host "Directory does not exist: $directory" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== End of File List ===" -ForegroundColor Green
