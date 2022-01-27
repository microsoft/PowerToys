[CmdletBinding()]
# todo: send in arch / conf, could send in actual path
Param(
  [Parameter(Mandatory=$True,Position=1)]
  [AllowEmptyString()]
  [string]$targetDir = $PSScriptRoot + '/../extractedMsi/File'
)

$DirPath = $targetDir;  #this file is in pipeline, we need root.
$items = Get-ChildItem -Path $DirPath -File -Include *.exe,*.dll,*.ttf -Recurse -Force -ErrorAction SilentlyContinue
$totalFailure = 0;

Write-Host $DirPath;

if(-not (Test-Path $DirPath))
{  
    Write-Host "Folder does not exist!"
}

Write-Host "Total items: " $items.Count

if($items.Count -eq 0)
{
	# no items means something bad happened.  We should fail ASAP
	exit 1;
}

$items | ForEach-Object { 
    if($_.VersionInfo.FileVersion -eq "1.0.0.0" )
	{
		if(-not $_.Name.EndsWith("Microsoft.Search.Interop.dll"))
		{
			Write-Host "Version not set: " + $_.FullName
			$totalFailure++;
		}
	}
}

$items | ForEach-Object { 
	$auth = Get-AuthenticodeSignature $_.FullName
	if($auth.SignerCertificate -eq $null)
	{
		Write-Host "Not Signed: " + $_.FullName
		$totalFailure++;
	}
}

if($totalFailure -gt 0)
{
	exit 1
}

exit 0
