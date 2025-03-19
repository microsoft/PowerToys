# Use me to check if extensions deployed correctly

$gitRoot = git rev-parse --show-toplevel
Write-output "Checking repo root at $gitroot"
$extensionsRoot = "$gitroot\x64\Debug\WinUI3Apps\CmdPalExtensions"
Get-ChildItem -Path $extensionsRoot | ForEach-Object {
    $extensionName = $_.Name
    Write-Host "`e[1m$extensionName`e[m"
    $extensionName
    $extensionAppx = $_.PsPath + "\Appx"
    if ((Test-Path $extensionAppx) -eq $false) {
        Write-Host "  `e[31;1mUNEXPECTED`e[0m: There was no Appx/ directory. Make sure you deploy the package!"
    }
    else
    {
        # Write-Host "`e[90m  Found Appx/ directory`e[0m"
        $appxWinmd = $extensionAppx + "\Microsoft.CommandPalette.Extensions.winmd"
        if ((Test-Path $appxWinmd) -eq $false) {
            Write-Host "  `e[31;1mUNEXPECTED`e[0m: Did not find Microsoft.CommandPalette.Extensions.winmd"
        }
        else {
            # Write-Host "`e[90m  Found the winmd`e[0m"
            Write-Host "`e[92m  Everything looks good`e[0m"
        }
    }
}

Write-Host "`e[1mChecking host apps:`e[m"
$hostAppxRoot = "$gitroot/x64/Debug/WinUI3Apps/CmdPal"
$hostAppxWinmd = $hostAppxRoot + "/Microsoft.CommandPalette.Extensions.winmd"
$prototypeAppxRoot = "$gitroot/x64/Debug/WinUI3Apps/CmdPal.Poc"
$prototypeAppxWinmd = $hostAppxRoot + "/Microsoft.CommandPalette.Extensions.winmd"
if ((Test-Path $hostAppxWinmd)) {
    Write-Host "  Found Microsoft.CommandPalette.Extensions.winmd in The Real App's Appx/"
}
else {
    Write-Host "  `e[31;1mUNEXPECTED`e[0m: Did not find Microsoft.CommandPalette.Extensions.winmd in The Real App's Appx/"
    Write-Host "    Go look in: "
    Write-Host "    start file://$hostAppxRoot"
}
if ((Test-Path $prototypeAppxWinmd)) {
    Write-Host "  Found Microsoft.CommandPalette.Extensions.winmd in the prototype's Appx/"
}
else {
    Write-Host "  `e[31;1mUNEXPECTED`e[0m: Did not find Microsoft.CommandPalette.Extensions.winmd in the prototype's Appx/"
    Write-Host "    Go look in: "
    Write-Host "    start file://$prototypeAppxRoot"
}

Write-Host "`e[1mChecking actual extension interface project output:`e[m"

$winmdRoot = "$gitroot/x64/Debug/Microsoft.CommandPalette.Extensions"
$winmdOutput = "$winmdRoot/Microsoft.CommandPalette.Extensions.winmd"

if ((Test-Path $winmdOutput)) {
    Write-Host "  Found Microsoft.CommandPalette.Extensions.winmd where it's built"
}
else {
    Write-Host "  `e[31;1mUNEXPECTED`e[0m: Did not find Microsoft.CommandPalette.Extensions.winmd where it's supposed to be built! Did you build Microsoft.CommandPalette.Extensions?"
    Write-Host "  Go look in: "
    Write-Host "  start file://$winmdRoot"
}
