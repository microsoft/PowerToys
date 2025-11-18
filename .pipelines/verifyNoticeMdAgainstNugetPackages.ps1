[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True,Position=1)]
    [string]$path
)

$noticeFile = Get-Content -Raw "NOTICE.md"

Write-Host $noticeFile

Write-Host "Verifying NuGet packages"

$projFiles = Get-ChildItem $path -Filter *.csproj -force -Recurse
$projFiles.Count

Write-Host "Going through all csproj files"

$totalList = $projFiles | ForEach-Object -Parallel {
    $csproj = $_
    $nugetTemp = @();
    
    #Workaround for preventing exit code from dotnet process from reflecting exit code in PowerShell
    $procInfo = New-Object System.Diagnostics.ProcessStartInfo -Property @{ 
        FileName               = "dotnet.exe"; 
        Arguments              = "list $csproj package"; 
        RedirectStandardOutput = $true; 
        RedirectStandardError  = $true; 
    }
    
    $proc = [System.Diagnostics.Process]::Start($procInfo);

    while (!$proc.StandardOutput.EndOfStream) {
        $nugetTemp += $proc.StandardOutput.ReadLine();
    }
    
    $proc = $null;
    $procInfo = $null;

    if($nugetTemp -is [array] -and $nugetTemp.count -gt 3)
    {
        # Need to debug this script? Uncomment this line.
        # Write-Host $csproj "`r`n" $nugetTemp "`r`n"
        $temp = New-Object System.Collections.ArrayList
        $temp.AddRange($nugetTemp)
        $temp.RemoveRange(0, 3)

        foreach($p in $temp) 
        {
            # ignore "Auto-referenced" string in the output
            if ($p -match "Auto-referenced") {
                continue
            }

            # breaking item down to usable array and getting 1 and 2, see below of a sample output
            #    > PACKAGE      VERSION            VERSION
            # if a package is Auto-referenced, "(A)" will appear in position 1 instead of a version number.

            $p = -split $p
            $p = $p[1, 2]
            $tempString = $p[0]

            if([string]::IsNullOrWhiteSpace($tempString))
            {
                Continue
            }

            if($tempString.StartsWith("Microsoft.") -Or $tempString.StartsWith("System."))
            {
                Continue
            }

            echo "- $tempString"
        }
	$csproj = $null;
    }
} -ThrottleLimit 4 | Sort-Object

$returnList = [System.Collections.Generic.HashSet[string]]($totalList) -join "`r`n"

Write-Host $returnList

# Extract the current package list from NOTICE.md
$noticePattern = "## NuGet Packages used by PowerToys\s*((?:\r?\n- .+)+)"
$noticeMatch = [regex]::Match($noticeFile, $noticePattern)

if ($noticeMatch.Success) {
    $currentNoticePackageList = $noticeMatch.Groups[1].Value.Trim()
} else {
    Write-Warning "Warning: Could not find 'NuGet Packages used by PowerToys' section in NOTICE.md"
    $currentNoticePackageList = ""
}

if (!$noticeFile.Trim().EndsWith($returnList.Trim()))
{
	Write-Host -ForegroundColor Red "Notice.md does not match NuGet list."

	# Show detailed differences
	$generatedPackages = $returnList -split "`r`n|`n" | Where-Object { $_.Trim() -ne "" } | Sort-Object
	$noticePackages = $currentNoticePackageList -split "`r`n|`n" | Where-Object { $_.Trim() -ne "" } | ForEach-Object { $_.Trim() } | Sort-Object

	Write-Host ""
	Write-Host -ForegroundColor Cyan "=== DETAILED DIFFERENCE ANALYSIS ==="
	Write-Host ""

	# Find packages in proj file list but not in NOTICE.md
	$missingFromNotice = $generatedPackages | Where-Object { $noticePackages -notcontains $_ }
	if ($missingFromNotice.Count -gt 0) {
		Write-Host -ForegroundColor Red "MissingFromNotice:"
		foreach ($pkg in $missingFromNotice) {
			Write-Host -ForegroundColor Red "  $pkg"
		}
		Write-Host ""
	}

	# Find packages in NOTICE.md but not in proj file list
	$extraInNotice = $noticePackages | Where-Object { $generatedPackages -notcontains $_ }
	if ($extraInNotice.Count -gt 0) {
		Write-Host -ForegroundColor Yellow "ExtraInNotice:"
		foreach ($pkg in $extraInNotice) {
			Write-Host -ForegroundColor Yellow "  $pkg"
		}
		Write-Host ""
	}

	# Show counts for summary
	Write-Host -ForegroundColor Cyan "Summary:"
	Write-Host "  Proj file list has $($generatedPackages.Count) packages"
	Write-Host "  NOTICE.md has $($noticePackages.Count) packages"
	Write-Host "  MissingFromNotice: $($missingFromNotice.Count) packages"
	Write-Host "  ExtraInNotice: $($extraInNotice.Count) packages"
	Write-Host ""

	exit 1
}

exit 0
