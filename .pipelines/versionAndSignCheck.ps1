[CmdletBinding()]
# todo: send in arch / conf
Param()

$DirPath = $PSScriptRoot + '/extractedMsi';
$items = Get-ChildItem -Path $DirPath -File -Include *.exe,*.dll -Recurse -Force -ErrorAction SilentlyContinue
$totalFailure = 0;

Write-Host $DirPath;
Write-Host "Total items:" $items.Count

$items | ForEach-Object { 
    if($_.VersionInfo.FileVersion -eq "0.0.1.0" -or $_.VersionInfo.FileVersion -eq "1.0.0.0" )
	{
		if($_.Name -ne "Microsoft.Search.Interop.dll")
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
