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
$totalList = New-Object System.Collections.ArrayList

Write-Host "Going through all csproj files"

$totalList = $projFiles | ForEach-Object -Parallel {
    $csproj = $_
    $nugetTemp = dotnet list $csproj package

    if($nugetTemp -is [array] -and $nugetTemp.count -gt 3)
    {
        $temp = New-Object System.Collections.ArrayList
        $temp.AddRange($nugetTemp)
        $temp.RemoveRange(0, 3)

        foreach($p in $temp) 
        {
            # breaking item down to usable array and getting 1 and 2, see below of a sample output
            #    > PACKAGE      VERSION            VERSION

            $p = -split $p
            $p = $p[1, 2]
            $tempString = $p[0] + " " + $p[1]

            if(![string]::IsNullOrWhiteSpace($tempString))
            {
		   echo "- $tempString";
            }
        }
	$csproj = $null;
    }
} -ThrottleLimit 4 | Sort-Object

$returnList = [System.Collections.Generic.HashSet[string]]($totalList) -join "`r`n"

Write-Host $returnList

if (!$noticeFile.Trim().EndsWith($returnList.Trim()))
{
	Write-Host -ForegroundColor Red "Notice.md does not match NuGet list."
	exit 1
}

exit 0
