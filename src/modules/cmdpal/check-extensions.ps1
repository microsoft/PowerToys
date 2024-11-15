# Use me to check if extensions deployed correctly

$gitRoot = git rev-parse --show-toplevel
Write-output "Checking repo root at $gitroot"
$extensionsRoot = "$gitroot\x64\Debug\WinUI3Apps\CmdPalExtensions"
Get-ChildItem -Path $extensionsRoot | ForEach-Object {
    $extensionName = $_.Name
    $extensionName
    $extensionAppx = $_.PsPath + "\Appx"
    if ((Test-Path $extensionAppx) -eq $false) {
        Write-Host "  `e[31;1mUNEXPECTED`e[0m: There was no Appx/ directory. Make sure you deploy the package!"
    }
    else 
    {
        # Write-Host "`e[90m  Found Appx/ directory`e[0m"
        $appxWinmd = $extensionAppx + "\Microsoft.CmdPal.Extensions.winmd"
        if ((Test-Path $appxWinmd) -eq $false) {
            Write-Host "  `e[31;1mUNEXPECTED`e[0m: Did not find Microsoft.CmdPal.Extensions.winmd"
        }
        else {
            # Write-Host "`e[90m  Found the winmd`e[0m"
            Write-Host "`e[92m  Everything looks good`e[0m"
        }
    }
}
$winmdRoot = "$gitroot\\x64\Debug\Microsoft.CmdPal.Extensions"
$winmdOutput = "$winmdRoot\Microsoft.CmdPal.Extensions.winmd"
if ((Test-Path $appxWinmd)) {
    Write-Host "Found Microsoft.CmdPal.Extensions.winmd where it's supposed to be"
}
else {
    Write-Host "  `e[31;1mUNEXPECTED`e[0m: Did not find Microsoft.CmdPal.Extensions.winmd where it's supposed to be built! Did you build Microsoft.CmdPal.Extensions?"    
}