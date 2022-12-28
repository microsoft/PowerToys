[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True,Position=1)]
    [string]$path
)

Write-Host "Verifying NuGet packages"

$projFiles = Get-ChildItem $path -Include *.csproj -force -Recurse
$projFiles.Count
$totalList = New-Object System.Collections.ArrayList

Write-Host "Going through all csproj files"

foreach($csproj in $projFiles) 
{
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
				$totalList.Add($tempString)
			}
		}
	}
}

Write-Host "Removing duplicates"

$totalList = $totalList | Sort-Object | Get-Unique
$returnList = ""

foreach($p in $totalList) 
{
	$returnList += "- " + $p + "`r`n"
}

Write-Host $returnList

# if (-not $?)
# {
#     Write-Host -ForegroundColor Red "Notice.md does not match NuGet list."
#     exit 1
# }

exit 0
