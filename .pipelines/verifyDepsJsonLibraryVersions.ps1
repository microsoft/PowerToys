[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$targetDir
)

# This script will check every deps.json file in the target directory to see if for each dll mentioned,
#all the deps.json files that mention it will mention the same version.
# The main goal is to catch when different versions for the same module might be copied to the same directory
#at build time and might create flaky builds that get the wrong version of the dll sometimes.

# A dictionary of dictionaries of lists to save which files reference each version of each dll.
# Logic is DllName > fileVersion > list with deps.json files that reference it.
# If for a specific dll there's more than one referenced file version, we have build collisions.
$referencedFileVersionsPerDll = @{}
$totalFailures = 0

Get-ChildItem $targetDir -Recurse -Filter *.deps.json -Exclude UITests-FancyZones*,MouseJump.Common.UnitTests*,AdvancedPaste.FuzzTests* | ForEach-Object {
    # Temporarily exclude FancyZones UI tests because of Appium.WebDriver dependencies
    $depsJsonFullFileName = $_.FullName
    $depsJsonFileName = $_.Name
    $depsJson = Get-Content $depsJsonFullFileName | ConvertFrom-Json

    # We're doing a breadth first search to look for every runtime object.
    $iterateThroughEveryField = New-Object System.Collections.Generic.Queue[System.Object]
    $iterateThroughEveryField.Enqueue($depsJson)

    while($iterateThroughEveryField.Count -gt 0)
    {
        $currentObject = $iterateThroughEveryField.Dequeue();
        $currentObject.PSObject.Properties | ForEach-Object {
            if($_.Name -ne 'SyncRoot') {
                # Skip SyncRoot to avoid looping in array objects.
                # Care only about objects, not value types.
                $iterateThroughEveryField.Enqueue($_.Value)
                if($_.Name -eq 'runtime')
                {
                    # Cycle through each dll.
                    $_.Value.PSObject.Properties | ForEach-Object {
                        if($_.Name.EndsWith('.dll')) {
                            $dllName = Split-Path $_.Name -leaf
                            if([bool]($_.Value.PSObject.Properties.name -match 'fileVersion')) {
                                $dllFileVersion = $_.Value.fileVersion
                                if (([string]::IsNullOrEmpty($dllFileVersion) -or ($dllFileVersion -eq '0.0.0.0')) -and $dllName.StartsWith('PowerToys.'))` {
                                    # After VS 17.11 update some of PowerToys dlls have no fileVersion in deps.json even though the 
                                    # version is correctly set. This is a workaround to skip our dlls as we are confident that all of
                                    # our dlls share the same version across the dependencies.
									# After VS 17.13 these error versions started appearing as 0.0.0.0 so we've added that case to the condition as well.
                                    continue
                                }

                                # Add the entry to the dictionary of dictionary of lists
                                if(-Not $referencedFileVersionsPerDll.ContainsKey($dllName)) {
                                    $referencedFileVersionsPerDll[$dllName] = @{ $dllFileVersion = New-Object System.Collections.Generic.List[System.String] }
                                } elseif(-Not $referencedFileVersionsPerDll[$dllName].ContainsKey($dllFileVersion)) {
                                    $referencedFileVersionsPerDll[$dllName][$dllFileVersion] = New-Object System.Collections.Generic.List[System.String]
                                }
                                $referencedFileVersionsPerDll[$dllName][$dllFileVersion].Add($depsJsonFileName)
                            }
                        }
                    }
                }
            }
        }
    }
}

# Report on the files that are referenced for more than one version.
$referencedFileVersionsPerDll.keys | ForEach-Object {
    if($referencedFileVersionsPerDll[$_].Count -gt 1) {
        $dllName = $_
        Write-Host $dllName
        $referencedFileVersionsPerDll[$dllName].keys | ForEach-Object {
            Write-Host "`t" $_ 
            $referencedFileVersionsPerDll[$dllName][$_] | ForEach-Object {
                Write-Host "`t`t" $_
            }
        }
        $totalFailures++;
    }
}

if ($totalFailures -gt 0) {
    Write-Host -ForegroundColor Red "Detected " $totalFailures " libraries that are mentioned with different version across the dependencies.`r`n"
    exit 1
}

Write-Host -ForegroundColor Green "All " $referencedFileVersionsPerDll.keys.Count " libraries are mentioned with the same version across the dependencies.`r`n"
exit 0

