# Fix XAML files - remove duplicates and fix formatting
$viewsPath = "src\settings-ui\Settings.UI\SettingsXAML\Views"
$xamlFiles = Get-ChildItem -Path $viewsPath -Filter "*.xaml"

Write-Host "Found $($xamlFiles.Count) XAML files to fix"
Write-Host ""

foreach ($file in $xamlFiles) {
    Write-Host "Processing: $($file.Name)"
    
    $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    $originalContent = $content
    
    # Remove malformed AutomationProperties.AutomationId entries
    $content = $content -replace '`r`n\s*AutomationProperties\.AutomationId="[^"]*"`r`n\s*AutomationProperties\.AutomationId="[^"]*"', ''
    $content = $content -replace '`r`n\s*AutomationProperties\.AutomationId="[^"]*"', ''
    
    # Now add proper AutomationProperties.AutomationId for each x:Uid
    $content = [regex]::Replace($content, '(\s+)(x:Uid="([^"]+)")', {
        param($match)
        $indent = $match.Groups[1].Value
        $uidAttr = $match.Groups[2].Value
        $uidValue = $match.Groups[3].Value
        
        return "$indent$uidAttr`r`n${indent}AutomationProperties.AutomationId=`"$uidValue`""
    })
    
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
        Write-Host "  Fixed"
    } else {
        Write-Host "  No changes needed"
    }
}

Write-Host ""
Write-Host "All files fixed!" 