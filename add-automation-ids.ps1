param(
    [string]$Path = "src/settings-ui/Settings.UI/SettingsXAML/Views"
)

# Function to process a single XAML file
function Add-AutomationIds {
    param(
        [string]$FilePath
    )
    
    Write-Host "Processing: $FilePath"
    
    # Read the file content
    $content = Get-Content -Path $FilePath -Raw -Encoding UTF8
    
    # Track if any changes were made
    $changed = $false
    
    # Pattern to match elements with x:Uid but without AutomationProperties.AutomationId
    # This regex looks for:
    # 1. Opening tag with x:Uid="something"
    # 2. Not already having AutomationProperties.AutomationId
    $pattern = '(<[^>]*?\s+x:Uid="([^"]+)"[^>]*?)(?![^>]*AutomationProperties\.AutomationId)([^>]*>)'
    
    # Replace function
    $newContent = [regex]::Replace($content, $pattern, {
        param($match)
        
        $beforeUid = $match.Groups[1].Value
        $uidValue = $match.Groups[2].Value
        $afterUid = $match.Groups[3].Value
        
        # Insert AutomationProperties.AutomationId right after x:Uid
        $replacement = $beforeUid + "`r`n    AutomationProperties.AutomationId=`"$uidValue`"" + $afterUid
        
        $script:changed = $true
        return $replacement
    })
    
    # Only write back if changes were made
    if ($changed) {
        Set-Content -Path $FilePath -Value $newContent -Encoding UTF8 -NoNewline
        Write-Host "  Updated with AutomationProperties.AutomationId attributes"
    } else {
        Write-Host "  No changes needed"
    }
}

# Get all XAML files in the specified directory
$xamlFiles = Get-ChildItem -Path $Path -Filter "*.xaml" -Recurse

Write-Host "Found $($xamlFiles.Count) XAML files to process"
Write-Host ""

# Process each file
foreach ($file in $xamlFiles) {
    Add-AutomationIds -FilePath $file.FullName
}

Write-Host ""
Write-Host "Processing complete!" 