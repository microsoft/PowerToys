# Batch update all XAML files in Views directory
$viewsPath = "src\settings-ui\Settings.UI\SettingsXAML\Views"
$xamlFiles = Get-ChildItem -Path $viewsPath -Filter "*.xaml"

Write-Host "Found $($xamlFiles.Count) XAML files to update"
Write-Host ""

foreach ($file in $xamlFiles) {
    Write-Host "Processing: $($file.Name)"
    
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    
    # Simple replacements for common patterns
    $originalContent = $content
    $content = $content -replace '(\s+x:Uid="([^"]+)")', '$1`r`n    AutomationProperties.AutomationId="$2"'
    
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
        Write-Host "  Updated"
    } else {
        Write-Host "  No changes needed"
    }
}

Write-Host ""
Write-Host "All files processed!" 